using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Katsudon.Editor.Udon;
using Katsudon.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using VRC.SDK3.Components;
using VRC.Udon;
using Object = UnityEngine.Object;

namespace Katsudon.Editor
{
	[CustomPropertyDrawer(typeof(UnityEventBase), true)]
	public class UnityEventDrawer : UnityEditorInternal.UnityEventDrawer
	{
		private const string kNoFunctionString = "No Function";

		//Persistent Listener Paths
		internal const string kInstancePath = "m_Target";
		internal const string kCallStatePath = "m_CallState";
		internal const string kArgumentsPath = "m_Arguments";
		internal const string kModePath = "m_Mode";
		internal const string kMethodNamePath = "m_MethodName";

		//ArgumentCache paths
		internal const string kFloatArgument = "m_FloatArgument";
		internal const string kIntArgument = "m_IntArgument";
		internal const string kObjectArgument = "m_ObjectArgument";
		internal const string kStringArgument = "m_StringArgument";
		internal const string kBoolArgument = "m_BoolArgument";
		internal const string kObjectArgumentAssemblyTypeName = "m_ObjectArgumentAssemblyTypeName";

		private static MethodInfo sendEventMethod = typeof(UdonBehaviour).GetMethod(nameof(UdonBehaviour.SendCustomEvent));

		private static Func<UnityEditorInternal.UnityEventDrawer, Rect, Rect[]> GetRowRectsFunc = CreateFunc<UnityEditorInternal.UnityEventDrawer, Rect, Rect[]>("GetRowRects");

		private static Func<UnityEditorInternal.UnityEventDrawer, SerializedProperty> ListenersArrayGetter = CreateGetter<UnityEditorInternal.UnityEventDrawer, SerializedProperty>("m_ListenersArray");
		private static Func<UnityEditorInternal.UnityEventDrawer, UnityEventBase> DummyEventGetter = CreateGetter<UnityEditorInternal.UnityEventDrawer, UnityEventBase>("m_DummyEvent");

		private static Func<string, bool> _isUdonBehaviourMethodAllowed = null;
		private static Func<string, bool> IsUdonBehaviourMethodAllowed
		{
			get
			{
				if(_isUdonBehaviourMethodAllowed == null)
				{
					Type filterType = null;
					foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
					{
						filterType = assembly.GetType("VRC.Core.UnityEventFilter", false);
						if(filterType != null) break;
					}
					var property = filterType.GetProperty("AllowedUnityEventTargetTypes", BindingFlags.NonPublic | BindingFlags.Static);
					var filter = ((IDictionary)property.GetValue(null))[typeof(UdonBehaviour)];
					_isUdonBehaviourMethodAllowed = (Func<string, bool>)Delegate.CreateDelegate(typeof(Func<string, bool>), filter, "IsTargetMethodAllowed");
				}
				return _isUdonBehaviourMethodAllowed;
			}
		}

		private SerializedProperty m_ListenersArray => ListenersArrayGetter(this);
		private UnityEventBase m_DummyEvent => DummyEventGetter(this);

		private Rect[] GetRowRects(Rect rect) => GetRowRectsFunc(this, rect);

		protected override void DrawEvent(Rect rect, int index, bool isActive, bool isFocused)
		{
			var pListener = m_ListenersArray.GetArrayElementAtIndex(index);

			rect.y++;
			Rect[] subRects = GetRowRects(rect);
			Rect enabledRect = subRects[0];
			Rect goRect = subRects[1];
			Rect functionRect = subRects[2];
			Rect argRect = subRects[3];

			// find the current event target...
			var callState = pListener.FindPropertyRelative(kCallStatePath);
			var mode = pListener.FindPropertyRelative(kModePath);
			var arguments = pListener.FindPropertyRelative(kArgumentsPath);
			var listenerTarget = pListener.FindPropertyRelative(kInstancePath);
			var methodName = pListener.FindPropertyRelative(kMethodNamePath);

			Color c = GUI.backgroundColor;
			GUI.backgroundColor = Color.white;

			EditorGUI.PropertyField(enabledRect, callState, GUIContent.none);

			EditorGUI.BeginChangeCheck();
			{
				GUI.Box(goRect, GUIContent.none);
				EditorGUI.PropertyField(goRect, listenerTarget, GUIContent.none);
				if(EditorGUI.EndChangeCheck())
					methodName.stringValue = null;
			}

			SerializedProperty argument;
			var modeEnum = GetMode(mode);
			//only allow argument if we have a valid target / method
			if(listenerTarget.objectReferenceValue == null || string.IsNullOrEmpty(methodName.stringValue))
				modeEnum = PersistentListenerMode.Void;

			switch(modeEnum)
			{
				case PersistentListenerMode.Float:
					argument = arguments.FindPropertyRelative(kFloatArgument);
					break;
				case PersistentListenerMode.Int:
					argument = arguments.FindPropertyRelative(kIntArgument);
					break;
				case PersistentListenerMode.Object:
					argument = arguments.FindPropertyRelative(kObjectArgument);
					break;
				case PersistentListenerMode.String:
					argument = arguments.FindPropertyRelative(kStringArgument);
					break;
				case PersistentListenerMode.Bool:
					argument = arguments.FindPropertyRelative(kBoolArgument);
					break;
				default:
					argument = arguments.FindPropertyRelative(kIntArgument);
					break;
			}

			var desiredArgTypeName = arguments.FindPropertyRelative(kObjectArgumentAssemblyTypeName).stringValue;
			var desiredType = typeof(Object);
			if(!string.IsNullOrEmpty(desiredArgTypeName))
				desiredType = Type.GetType(desiredArgTypeName, false) ?? typeof(Object);

			if(modeEnum == PersistentListenerMode.Object)
			{
				EditorGUI.BeginChangeCheck();
				var result = EditorGUI.ObjectField(argRect, GUIContent.none, argument.objectReferenceValue, desiredType, true);
				if(EditorGUI.EndChangeCheck())
					argument.objectReferenceValue = result;
			}
			else if(modeEnum != PersistentListenerMode.Void && modeEnum != PersistentListenerMode.EventDefined)
				EditorGUI.PropertyField(argRect, argument, GUIContent.none);

			using(new EditorGUI.DisabledScope(listenerTarget.objectReferenceValue == null))
			{
				EditorGUI.BeginProperty(functionRect, GUIContent.none, methodName);
				{
					GUIContent buttonContent;
					if(EditorGUI.showMixedValue)
					{
						buttonContent = EditorGUIUtility.TrTextContent("—", "Mixed Values");
					}
					else
					{
						var buttonLabel = new StringBuilder();
						if(listenerTarget.objectReferenceValue == null || string.IsNullOrEmpty(methodName.stringValue))
						{
							buttonLabel.Append(kNoFunctionString);
						}
						else if(!IsPersistantListenerValid(m_DummyEvent, methodName.stringValue, listenerTarget.objectReferenceValue, GetMode(mode), desiredType))
						{
							var instanceString = "UnknownComponent";
							var instance = listenerTarget.objectReferenceValue;
							if(instance != null)
								instanceString = instance.GetType().Name;

							buttonLabel.Append(string.Format("<Missing {0}.{1}>", instanceString, methodName.stringValue));
						}
						else
						{
							MonoBehaviour proxy;
							if(listenerTarget.objectReferenceValue is UdonBehaviour ubeh && (proxy = ProxyUtils.GetProxyByBehaviour(ubeh)) != null)
							{
								buttonLabel.Append(proxy.GetType().Name);
							}
							else
							{
								buttonLabel.Append(listenerTarget.objectReferenceValue.GetType().Name);
							}

							if(!string.IsNullOrEmpty(methodName.stringValue))
							{
								buttonLabel.Append(".");
								if(methodName.stringValue.StartsWith("set_"))
									buttonLabel.Append(methodName.stringValue.Substring(4));
								else
									buttonLabel.Append(methodName.stringValue);
							}
						}
						buttonContent = new GUIContent(buttonLabel.ToString());
					}

					if(GUI.Button(functionRect, buttonContent, EditorStyles.popup))
						BuildPopupList(listenerTarget.objectReferenceValue, m_DummyEvent, pListener).DropDown(functionRect);
				}
				EditorGUI.EndProperty();
			}
			GUI.backgroundColor = c;
		}

		static GenericMenu BuildPopupList(Object target, UnityEventBase dummyEvent, SerializedProperty listener)
		{
			//special case for components... we want all the game objects targets there!
			var targetToUse = target;
			if(targetToUse is Component)
				targetToUse = (target as Component).gameObject;

			// find the current event target...
			var methodName = listener.FindPropertyRelative(kMethodNamePath);

			var menu = new GenericMenu();
			menu.AddItem(new GUIContent(kNoFunctionString),
				string.IsNullOrEmpty(methodName.stringValue),
				ClearEventFunction,
				new UnityEventFunction(listener, null, null, PersistentListenerMode.EventDefined, null));

			if(targetToUse == null)
				return menu;

			menu.AddSeparator("");

			// figure out the signature of this delegate...
			// The property at this stage points to the 'container' and has the field name
			Type delegateType = dummyEvent.GetType();

			// check out the signature of invoke as this is the callback!
			MethodInfo delegateMethod = delegateType.GetMethod("Invoke");
			var delegateArgumentsTypes = delegateMethod.GetParameters().Select(x => x.ParameterType).ToArray();

			GeneratePopUpForType(menu, targetToUse, false, listener, delegateArgumentsTypes);
			if(targetToUse is GameObject)
			{
				Component[] comps = (targetToUse as GameObject).GetComponents<Component>();
				var duplicateNames = comps.Where(c => c != null).Select(c => c.GetType().Name).GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
				foreach(Component comp in comps)
				{
					if(comp == null || Utils.IsUdonAsm(comp.GetType()) || comp is ReferencesContainer) continue;
					if(comp is UdonBehaviour ubeh)
					{
						var proxy = ProxyUtils.GetProxyByBehaviour(ubeh);
						if(proxy != null)
						{
							GeneratePopUpForProxy(menu, ubeh, proxy, duplicateNames.Contains(comp.GetType().Name), listener, delegateArgumentsTypes);
							continue;
						}
					}
					GeneratePopUpForType(menu, comp, duplicateNames.Contains(comp.GetType().Name), listener, delegateArgumentsTypes);
				}
			}

			return menu;
		}

		private static void GeneratePopUpForProxy(GenericMenu menu, UdonBehaviour ubeh, MonoBehaviour proxy,
			bool useFullTargetName, SerializedProperty listener, Type[] delegateArgumentsTypes)
		{
			var methods = new List<ValidMethodMap>();
			string targetName = useFullTargetName ? proxy.GetType().FullName : proxy.GetType().Name;

			bool didAddDynamic = false;
			// skip 'void' event defined on the GUI as we have a void prebuilt type!
			if(delegateArgumentsTypes.Length != 0)
			{
				GetMethodsForTargetAndMode(ubeh, delegateArgumentsTypes, methods, PersistentListenerMode.EventDefined);
				FilterUdonMethods(methods);
				if(methods.Count > 0)
				{
					menu.AddDisabledItem(new GUIContent(targetName + "/Dynamic " + string.Join(", ", Array.ConvertAll(delegateArgumentsTypes, e => GetTypeName(e)))));
					AddMethodsToMenu(menu, listener, methods, targetName);
					didAddDynamic = true;
				}
			}

			methods.Clear();
			GetMethodsForProxy(ubeh, proxy, methods);
			bool didAddCalls = false;
			if(methods.Count > 0)
			{
				if(didAddDynamic)
				{
					didAddDynamic = false;
					// AddSeperator doesn't seem to work for sub-menus, so we have to use this workaround instead of a proper separator for now.
					menu.AddItem(new GUIContent(targetName + "/ "), false, null);
				}

				menu.AddDisabledItem(new GUIContent(targetName + "/Send Udon Event:"));
				AddMethodsToMenu(menu, listener, methods, targetName);
				didAddCalls = true;
			}

			methods.Clear();
			GetMethodsForTargetAndMode(ubeh, new[] { typeof(float) }, methods, PersistentListenerMode.Float);
			GetMethodsForTargetAndMode(ubeh, new[] { typeof(int) }, methods, PersistentListenerMode.Int);
			GetMethodsForTargetAndMode(ubeh, new[] { typeof(string) }, methods, PersistentListenerMode.String);
			GetMethodsForTargetAndMode(ubeh, new[] { typeof(bool) }, methods, PersistentListenerMode.Bool);
			GetMethodsForTargetAndMode(ubeh, new[] { typeof(Object) }, methods, PersistentListenerMode.Object);
			GetMethodsForTargetAndMode(ubeh, new Type[] { }, methods, PersistentListenerMode.Void);
			if(methods.Count > 0)
			{
				if(didAddDynamic || didAddCalls)
				{
					// AddSeperator doesn't seem to work for sub-menus, so we have to use this workaround instead of a proper separator for now.
					menu.AddItem(new GUIContent(targetName + "/ "), false, null);
				}
				if(delegateArgumentsTypes.Length != 0)
				{
					menu.AddDisabledItem(new GUIContent(targetName + "/Static Parameters"));
				}
				FilterUdonMethods(methods);
				AddMethodsToMenu(menu, listener, methods, targetName);
			}
		}

		private static void FilterUdonMethods(List<ValidMethodMap> methods)
		{
			methods.RemoveAll(m => !IsUdonBehaviourMethodAllowed(m.methodInfo.Name));
		}

		private static void GetMethodsForProxy(UdonBehaviour ubeh, MonoBehaviour proxy, List<ValidMethodMap> methods)
		{
			// find the methods on the behaviour that match the signature
			var componentMethods = new List<MethodInfo>();

			var usedNames = new HashSet<string>();
			var type = proxy.GetType();
			while(type != typeof(MonoBehaviour))
			{
				var list = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
				for(int i = 0; i < list.Length; i++)
				{
					if(list[i].IsSpecialName) continue;
					usedNames.Add(list[i].Name);
				}
				type = type.BaseType;
			}

			foreach(var name in usedNames)
			{
				methods.Add(new UdonEventMethod(name, ubeh));
			}
		}

		private static void GeneratePopUpForType(GenericMenu menu, Object target, bool useFullTargetName, SerializedProperty listener, Type[] delegateArgumentsTypes)
		{
			var methods = new List<ValidMethodMap>();
			string targetName = useFullTargetName ? target.GetType().FullName : target.GetType().Name;

			bool didAddDynamic = false;

			// skip 'void' event defined on the GUI as we have a void prebuilt type!
			if(delegateArgumentsTypes.Length != 0)
			{
				GetMethodsForTargetAndMode(target, delegateArgumentsTypes, methods, PersistentListenerMode.EventDefined);
				if(methods.Count > 0)
				{
					menu.AddDisabledItem(new GUIContent(targetName + "/Dynamic " + string.Join(", ", delegateArgumentsTypes.Select(e => GetTypeName(e)).ToArray())));
					AddMethodsToMenu(menu, listener, methods, targetName);
					didAddDynamic = true;
				}
			}

			methods.Clear();
			GetMethodsForTargetAndMode(target, new[] { typeof(float) }, methods, PersistentListenerMode.Float);
			GetMethodsForTargetAndMode(target, new[] { typeof(int) }, methods, PersistentListenerMode.Int);
			GetMethodsForTargetAndMode(target, new[] { typeof(string) }, methods, PersistentListenerMode.String);
			GetMethodsForTargetAndMode(target, new[] { typeof(bool) }, methods, PersistentListenerMode.Bool);
			GetMethodsForTargetAndMode(target, new[] { typeof(Object) }, methods, PersistentListenerMode.Object);
			GetMethodsForTargetAndMode(target, new Type[] { }, methods, PersistentListenerMode.Void);
			if(methods.Count > 0)
			{
				if(didAddDynamic)
					// AddSeperator doesn't seem to work for sub-menus, so we have to use this workaround instead of a proper separator for now.
					menu.AddItem(new GUIContent(targetName + "/ "), false, null);
				if(delegateArgumentsTypes.Length != 0)
					menu.AddDisabledItem(new GUIContent(targetName + "/Static Parameters"));
				AddMethodsToMenu(menu, listener, methods, targetName);
			}
		}

		private static void AddMethodsToMenu(GenericMenu menu, SerializedProperty listener, List<ValidMethodMap> methods, string targetName)
		{
			// Note: sorting by a bool in OrderBy doesn't seem to work for some reason, so using numbers explicitly.
			IEnumerable<ValidMethodMap> orderedMethods = methods.OrderBy(e => e.methodInfo.Name.StartsWith("set_") ? 0 : 1).ThenBy(e => e.methodInfo.Name);
			foreach(var validMethod in orderedMethods)
				AddFunctionsForScript(menu, listener, validMethod, targetName);
		}

		static void AddFunctionsForScript(GenericMenu menu, SerializedProperty listener, ValidMethodMap method, string targetName)
		{
			PersistentListenerMode mode = method.mode;

			// find the current event target...
			var listenerTarget = listener.FindPropertyRelative(kInstancePath).objectReferenceValue;
			var methodName = listener.FindPropertyRelative(kMethodNamePath).stringValue;
			var setMode = GetMode(listener.FindPropertyRelative(kModePath));
			var arguments = listener.FindPropertyRelative(kArgumentsPath);
			var typeName = arguments.FindPropertyRelative(kObjectArgumentAssemblyTypeName);

			var args = new StringBuilder();
			var count = method.methodInfo.GetParameters().Length;
			for(int index = 0; index < count; index++)
			{
				var methodArg = method.methodInfo.GetParameters()[index];
				args.Append(string.Format("{0}", GetTypeName(methodArg.ParameterType)));

				if(index < count - 1)
					args.Append(", ");
			}

			var isCurrentlySet = listenerTarget == method.target
				&& methodName == method.methodInfo.Name
				&& mode == setMode;

			if(isCurrentlySet && mode == PersistentListenerMode.Object && method.methodInfo.GetParameters().Length == 1)
			{
				isCurrentlySet &= (method.methodInfo.GetParameters()[0].ParameterType.AssemblyQualifiedName == typeName.stringValue);
			}

			var eventMethod = method as UdonEventMethod;
			bool isUdonEvent = eventMethod != null;
			if(isCurrentlySet && isUdonEvent)
			{
				isCurrentlySet = arguments.FindPropertyRelative(kStringArgument).stringValue == eventMethod.name;
			}

			string path = GetFormattedMethodName(targetName, method, args.ToString());
			menu.AddItem(new GUIContent(path), isCurrentlySet, SetEventFunction,
				new UnityEventFunction(listener, method.target, method.methodInfo, mode, isUdonEvent ? eventMethod.name : null));
		}

		private static void GetMethodsForTargetAndMode(Object target, Type[] delegateArgumentsTypes, List<ValidMethodMap> methods, PersistentListenerMode mode)
		{
			IEnumerable<ValidMethodMap> newMethods = CalculateMethodMap(target, delegateArgumentsTypes, mode == PersistentListenerMode.Object);
			foreach(var m in newMethods)
			{
				var method = m;
				method.mode = mode;
				methods.Add(method);
			}
		}

		static IEnumerable<ValidMethodMap> CalculateMethodMap(Object target, Type[] t, bool allowSubclasses)
		{
			var validMethods = new List<ValidMethodMap>();
			if(target == null || t == null)
				return validMethods;

			// find the methods on the behaviour that match the signature
			Type componentType = target.GetType();
			var componentMethods = componentType.GetMethods().Where(x => !x.IsSpecialName).ToList();

			var wantedProperties = componentType.GetProperties().AsEnumerable();
			wantedProperties = wantedProperties.Where(x => x.GetCustomAttributes(typeof(ObsoleteAttribute), true).Length == 0 && x.GetSetMethod() != null);
			componentMethods.AddRange(wantedProperties.Select(x => x.GetSetMethod()));

			foreach(var componentMethod in componentMethods)
			{
				//Debug.Log ("Method: " + componentMethod);
				// if the argument length is not the same, no match
				var componentParamaters = componentMethod.GetParameters();
				if(componentParamaters.Length != t.Length)
					continue;

				// Don't show obsolete methods.
				if(componentMethod.GetCustomAttributes(typeof(ObsoleteAttribute), true).Length > 0)
					continue;

				if(componentMethod.ReturnType != typeof(void))
					continue;

				// if the argument types do not match, no match
				bool paramatersMatch = true;
				for(int i = 0; i < t.Length; i++)
				{
					if(!componentParamaters[i].ParameterType.IsAssignableFrom(t[i]))
						paramatersMatch = false;

					if(allowSubclasses && t[i].IsAssignableFrom(componentParamaters[i].ParameterType))
						paramatersMatch = true;
				}

				// valid method
				if(paramatersMatch)
				{
					var vmm = new ValidMethodMap(target, componentMethod);
					validMethods.Add(vmm);
				}
			}
			return validMethods;
		}

		private static string GetTypeName(Type t)
		{
			if(t == typeof(int))
				return "int";
			if(t == typeof(float))
				return "float";
			if(t == typeof(string))
				return "string";
			if(t == typeof(bool))
				return "bool";
			return t.Name;
		}

		static string GetFormattedMethodName(string targetName, ValidMethodMap method, string args)
		{
			if(method is UdonEventMethod eventMethod)
			{
				return string.Format("{0}/{1}", targetName, eventMethod.name);
			}
			var methodName = method.methodInfo.Name;
			if(method.mode == PersistentListenerMode.EventDefined)
			{
				if(methodName.StartsWith("set_"))
					return string.Format("{0}/{1}", targetName, methodName.Substring(4));
				else
					return string.Format("{0}/{1}", targetName, methodName);
			}
			else
			{
				if(methodName.StartsWith("set_"))
					return string.Format("{0}/{2} {1}", targetName, methodName.Substring(4), args);
				else
					return string.Format("{0}/{1} ({2})", targetName, methodName, args);
			}
		}

		private static void SetEventFunction(object source)
		{
			((UnityEventFunction)source).Assign();
		}

		private static void ClearEventFunction(object source)
		{
			((UnityEventFunction)source).Clear();
		}

		private static PersistentListenerMode GetMode(SerializedProperty mode)
		{
			return (PersistentListenerMode)mode.enumValueIndex;
		}

		private static Func<TDecl, TIn, TOut> CreateFunc<TDecl, TIn, TOut>(string name)
		{
			var target = Expression.Parameter(typeof(TDecl));
			var arg0 = Expression.Parameter(typeof(TIn));
			return Expression.Lambda<Func<TDecl, TIn, TOut>>(Expression.Call(target, typeof(TDecl).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance, null, new Type[] { typeof(TIn) }, null), arg0), target, arg0).Compile();
		}

		private static Func<TDecl, TOut> CreateGetter<TDecl, TOut>(string name)
		{
			var target = Expression.Parameter(typeof(TDecl));
			return Expression.Lambda<Func<TDecl, TOut>>(Expression.Field(target, typeof(TDecl).GetField(name, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)), target).Compile();
		}

		private class ValidMethodMap
		{
			public readonly Object target;
			public readonly MethodInfo methodInfo;
			public PersistentListenerMode mode;

			public ValidMethodMap(Object target, MethodInfo methodInfo)
			{
				this.target = target;
				this.methodInfo = methodInfo;
			}
		}

		private class UdonEventMethod : ValidMethodMap
		{
			public readonly string name;

			public UdonEventMethod(string name, Object target) : base(target, sendEventMethod)
			{
				this.name = name;
				this.mode = PersistentListenerMode.String;
			}
		}

		private struct UnityEventFunction
		{
			readonly SerializedProperty m_Listener;
			readonly Object m_Target;
			readonly MethodInfo m_Method;
			readonly PersistentListenerMode m_Mode;
			private readonly string m_StringValue;

			public UnityEventFunction(SerializedProperty listener, Object target, MethodInfo method, PersistentListenerMode mode, string stringValue)
			{
				m_Listener = listener;
				m_Target = target;
				m_Method = method;
				m_Mode = mode;
				m_StringValue = stringValue;
			}

			public void Assign()
			{
				// find the current event target...
				var listenerTarget = m_Listener.FindPropertyRelative(kInstancePath);
				var methodName = m_Listener.FindPropertyRelative(kMethodNamePath);
				var mode = m_Listener.FindPropertyRelative(kModePath);
				var arguments = m_Listener.FindPropertyRelative(kArgumentsPath);

				listenerTarget.objectReferenceValue = m_Target;
				methodName.stringValue = m_Method.Name;
				mode.enumValueIndex = (int)m_Mode;

				if(m_Mode == PersistentListenerMode.Object)
				{
					var fullArgumentType = arguments.FindPropertyRelative(kObjectArgumentAssemblyTypeName);
					var argParams = m_Method.GetParameters();
					if(argParams.Length == 1 && typeof(Object).IsAssignableFrom(argParams[0].ParameterType))
						fullArgumentType.stringValue = argParams[0].ParameterType.AssemblyQualifiedName;
					else
						fullArgumentType.stringValue = typeof(Object).AssemblyQualifiedName;
				}

				ValidateObjectParamater(arguments, m_Mode);

				if(m_Mode == PersistentListenerMode.String)
				{
					if(!string.IsNullOrEmpty(m_StringValue))
					{
						arguments.FindPropertyRelative(kStringArgument).stringValue = m_StringValue;
					}
				}

				m_Listener.serializedObject.ApplyModifiedProperties();
			}

			private void ValidateObjectParamater(SerializedProperty arguments, PersistentListenerMode mode)
			{
				var fullArgumentType = arguments.FindPropertyRelative(kObjectArgumentAssemblyTypeName);
				var argument = arguments.FindPropertyRelative(kObjectArgument);
				var argumentObj = argument.objectReferenceValue;

				if(mode != PersistentListenerMode.Object)
				{
					fullArgumentType.stringValue = typeof(Object).AssemblyQualifiedName;
					argument.objectReferenceValue = null;
					return;
				}

				if(argumentObj == null)
					return;

				Type t = Type.GetType(fullArgumentType.stringValue, false);
				if(!typeof(Object).IsAssignableFrom(t) || !t.IsInstanceOfType(argumentObj))
					argument.objectReferenceValue = null;
			}

			public void Clear()
			{
				// find the current event target...
				var methodName = m_Listener.FindPropertyRelative(kMethodNamePath);
				methodName.stringValue = null;

				var mode = m_Listener.FindPropertyRelative(kModePath);
				mode.enumValueIndex = (int)PersistentListenerMode.Void;

				m_Listener.serializedObject.ApplyModifiedProperties();
			}
		}
	}
}