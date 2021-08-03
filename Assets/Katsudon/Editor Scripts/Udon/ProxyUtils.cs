using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.ProgramSources;

namespace Katsudon.Editor.Udon
{
	public static class ProxyUtils
	{
		private static UdonValueResolver _valueResolver = null;
		public static UdonValueResolver valueResolver => _valueResolver ?? (_valueResolver = new UdonValueResolver());

		private static Func<UdonBehaviour, bool> isInitializedGetter = null;

		public static MonoBehaviour GetProxyByProgram(UdonBehaviour behaviour, SerializedUdonProgramAsset program)
		{
			var script = ProgramUtils.GetScriptByProgram(program);
			if(script == null) return null;
			return GetOrCreateProxy(behaviour, script);
		}

		public static MonoBehaviour GetProxyByBehaviour(UdonBehaviour behaviour)
		{
			if(behaviour.programSource != null) return null;
			var program = ProgramUtils.ProgramFieldGetter(behaviour);
			if(program == null) return null;
			return GetProxyByProgram(behaviour, program);
		}

		public static UdonBehaviour GetBehaviourByProxy(MonoBehaviour proxy)
		{
			if(BehavioursTracker.TryGetContainer(proxy.gameObject.scene, out var container) && container.TryGetBehaviour(proxy, out var behaviour))
			{
				return behaviour;
			}
			return null;
		}

		internal static MonoBehaviour GetOrCreateProxy(UdonBehaviour behaviour, MonoScript script)
		{
			if(PrefabUtility.IsPartOfPrefabAsset(behaviour)) return null;

			MonoBehaviour proxy = null;
			var container = BehavioursTracker.GetOrCreateContainer(behaviour.gameObject.scene);
			if(!container.TryGetProxy(behaviour, out proxy) || proxy.GetType() != script.GetClass())
			{
				if(proxy != null) container.RemoveBehaviour(behaviour);
				proxy = CreateProxy(behaviour, script);
				container.AddBehaviour(behaviour, proxy);
			}
			return proxy;
		}

		internal static UdonBehaviour GetOrCreateBehaviour(MonoBehaviour proxy)
		{
			UdonBehaviour behaviour = null;
			var scene = proxy.gameObject.scene;
			var container = BehavioursTracker.GetOrCreateContainer(scene);
			if(!container.TryGetBehaviour(proxy, out behaviour))
			{
				proxy.enabled = false;
				proxy.hideFlags = BehavioursTracker.SERVICE_OBJECT_FLAGS;

				behaviour = proxy.gameObject.AddComponent<UdonBehaviour>();
				container.AddBehaviour(behaviour, proxy);

				var programAsset = ProgramUtils.GetProgramByBehaviour(proxy);
				behaviour.AssignProgramAndVariables(programAsset, CreateVariableTable(programAsset.RetrieveProgram()));

				CopyFieldsToBehaviour(proxy, behaviour);

				if(EditorApplication.isPlaying && scene.IsValid() && !EditorSceneManager.IsPreviewScene(scene))
				{
					UdonManager.Instance.RegisterUdonBehaviour(behaviour);
				}
			}
			return behaviour;
		}

		internal static MonoBehaviour CreateProxy(UdonBehaviour behaviour, MonoScript script)
		{
			var container = BehavioursTracker.GetOrCreateContainer(behaviour.gameObject.scene);
			var proxy = container.CreateProxy(behaviour, script);
			CopyFieldsToProxy(behaviour, proxy);
			return proxy;
		}

		public static void CopyFieldsToBehaviour(MonoBehaviour proxy, UdonBehaviour behaviour)
		{
			var programAsset = ProgramUtils.ProgramFieldGetter(behaviour);
			if(programAsset != null)
			{
				var proxyType = proxy.GetType();
				var program = programAsset.RetrieveProgram();
				var symbols = program.SymbolTable;
				var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
				if(IsInitialized(behaviour))
				{
					foreach(var symbol in symbols.GetExportedSymbols())
					{
						var field = proxyType.GetField(symbol, flags);
						if(field != null && valueResolver.TryConvertToUdon(field.GetValue(proxy), out var converted))
						{
							behaviour.SetProgramVariable(symbol, converted);
						}
					}
				}
				else
				{
					behaviour.publicVariables = CreateVariableTable(program);
					foreach(var symbol in symbols.GetExportedSymbols())
					{
						var field = proxyType.GetField(symbol, flags);
						if(field != null && valueResolver.TryConvertToUdon(field.GetValue(proxy), out var converted))
						{
							behaviour.publicVariables.TrySetVariableValue(symbol, converted);
						}
					}
				}
			}
		}

		public static void CopyFieldsToProxy(UdonBehaviour behaviour, MonoBehaviour proxy)
		{
			var proxyType = proxy.GetType();
			var variables = behaviour.publicVariables;
			var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			if(IsInitialized(behaviour))
			{
				foreach(var symbol in variables.VariableSymbols)
				{
					if(behaviour.TryGetProgramVariable(symbol, out var value))
					{
						var field = proxyType.GetField(symbol, flags);
						if(field != null && valueResolver.TryConvertFromUdon(value, field.FieldType, out var converted))
						{
							field.SetValue(proxy, converted);
						}
					}
				}
			}
			else
			{
				foreach(var symbol in variables.VariableSymbols)
				{
					if(behaviour.publicVariables.TryGetVariableValue(symbol, out var value))
					{
						var field = proxyType.GetField(symbol, flags);
						if(field != null && valueResolver.TryConvertFromUdon(value, field.FieldType, out var converted))
						{
							field.SetValue(proxy, converted);
						}
					}
				}
			}
		}

		private static IUdonVariableTable CreateVariableTable(IUdonProgram program)
		{
			var symbols = program.SymbolTable;
			var heap = program.Heap;
			List<IUdonVariable> variables = new List<IUdonVariable>();//FIX: cache
			foreach(var symbol in symbols.GetExportedSymbols())
			{
				variables.Add(CreateUdonVariable(symbol, symbols.GetSymbolType(symbol)));
			}
			return new UdonVariableTable(variables);
		}

		private static IUdonVariable CreateUdonVariable(string symbolName, Type declaredType)
		{
			Type udonVariableType = typeof(UdonVariable<>).MakeGenericType(declaredType);
			return (IUdonVariable)Activator.CreateInstance(udonVariableType, symbolName, declaredType.IsValueType ? Activator.CreateInstance(declaredType) : null);
		}
		private static bool IsInitialized(UdonBehaviour udonBehaviour)
		{
			if(isInitializedGetter == null)
			{
				var behaviour = Expression.Parameter(typeof(UdonBehaviour));
				isInitializedGetter = Expression.Lambda<Func<UdonBehaviour, bool>>(
					Expression.Field(behaviour, typeof(UdonBehaviour).GetField("_initialized", BindingFlags.Instance | BindingFlags.NonPublic)),
				behaviour).Compile();
			}
			return isInitializedGetter.Invoke(udonBehaviour);
		}
	}
}