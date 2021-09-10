using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Katsudon.Reflection;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Editor;
using VRC.Udon.EditorBindings;
using VRC.Udon.Graph;
using VRC.Udon.Graph.Interfaces;
using VRC.Udon.Graph.NodeRegistries;
using VRC.Udon.UAssembly.Assembler;
using VRC.Udon.UAssembly.Interfaces;
using VRC.Udon.Wrapper;

namespace Katsudon.Builder.Helpers
{
	internal class UdonPartsCollector : UdonPartsCacheBase
	{
		public override string version => throw new NotImplementedException();

		public IReadOnlyDictionary<Type, int> GetTypeIdentifiers()
		{
			return typeIdentifiers;
		}

		protected override void CreateTypesList()
		{
			var udonInterface = new TypeAccess<UdonEditorManager>(UdonEditorManager.Instance).GetInstanceField<UdonEditorInterface>("_udonEditorInterface");
			var group = new TypeAccess<UdonEditorInterface>(udonInterface).GetInstanceField<TypeResolverGroup>("_typeResolverGroup");
			List<IUAssemblyTypeResolver> list = new TypeAccess<TypeResolverGroup>(group).GetInstanceField<List<IUAssemblyTypeResolver>>("_registeredTypeResolvers");

			var typeNames = new Dictionary<Type, string>();
			var typesProperty = typeof(BaseTypeResolver).GetProperty("Types", BindingFlags.NonPublic | BindingFlags.Instance);
			foreach(var resolver in list)
			{
				if(resolver is BaseTypeResolver)
				{
					var types = new PropertyAccess<IReadOnlyDictionary<string, Type>>(typesProperty, resolver).value;
					foreach(var pair in types)
					{
						if(pair.Key.EndsWith("Ref") && !pair.Value.Name.EndsWith("Ref"))
						{
							typeNames[pair.Value.MakeByRefType()] = pair.Key;
						}
						else if(!pair.Key.EndsWith("[]"))
						{
							typeNames[pair.Value] = pair.Key;
						}
					}
				}
			}
			this.typeNames = typeNames;
		}

		protected override void CreateMethodsList()
		{
			ParseUdonGraph();
		}

		protected override void CreateMethodBaseTypesList()
		{
			ParseUdonGraph();
		}

		protected override void CreateCtorsList()
		{
			ParseUdonGraph();
		}

		protected override void CreateFieldsList()
		{
			ParseUdonGraph();
		}

		protected override void CreateMagicMethodsList()
		{
			ParseUdonGraph();
		}

		protected override void CreateTypeIdentifiersList()
		{
			tmpTypeCounter = 0;
			typeIdentifiers = new Dictionary<Type, int>();
		}

		private void ParseUdonGraph()
		{
			var specialNodeNames = new HashSet<string>();
			foreach(var node in (new UdonSpecialNodeRegistry() as INodeRegistry).GetNodeDefinitions())
			{
				specialNodeNames.Add(node.fullName);
			}

			var parameters = new List<KeyValuePair<string, Type>>();

			var methods = new Dictionary<MethodIdentifier, MethodNodeInfo>();
			var fields = new Dictionary<FieldIdentifier, FieldNodeInfo>();
			var constructors = new Dictionary<MethodIdentifier, string>();
			var magicMethods = new Dictionary<string, MagicMethodInfo>();

			var wrapperAccess = new TypeAccess<UdonWrapper>(UdonEditorManager.Instance.GetWrapper() as UdonWrapper);
			var modules = wrapperAccess.GetInstanceField<IReadOnlyDictionary<string, IUdonWrapperModule>>("_wrapperModulesByName").value;

			var typeNames = GetTypeNames();
			var typeMembers = new Dictionary<string, MemberInfo>();
			var cachedSb = new StringBuilder();
			foreach(var module in modules)
			{
				var type = UdonEditorManager.Instance.GetTypeFromTypeString(module.Key);
				if(type != null)
				{
					var actions = new TypeAccess(module.Value.GetType(), module.Value)
						.GetInstanceField<IReadOnlyDictionary<string, int>>("_parameterCounts").value;

					typeMembers.Clear();
					CollectTypeMembers(type, cachedSb, typeMembers, typeNames);
					foreach(var action in actions)
					{
						if(typeMembers.TryGetValue(action.Key, out var memberInfo))
						{
							if(memberInfo is FieldInfo fieldInfo)
							{
								var id = new FieldIdentifier(this, fieldInfo);
								FieldNodeInfo nodeInfo;
								if(fields.TryGetValue(id, out nodeInfo))
								{
									if(type.IsAssignableFrom(nodeInfo.declaringType))
									{
										nodeInfo.declaringType = type;
									}
								}
								else
								{
									nodeInfo = new FieldNodeInfo(type);
								}
								if(action.Key.StartsWith("__get_")) nodeInfo.fullGetName = module.Key + "." + action.Key;
								else nodeInfo.fullSetName = module.Key + "." + action.Key;
								fields[id] = nodeInfo;
								continue;
							}
							if(memberInfo is ConstructorInfo ctorInfo)
							{
								constructors.Add(new MethodIdentifier(this, ctorInfo), module.Key + "." + action.Key);
								continue;
							}
							if(memberInfo is MethodInfo methodInfo)
							{
								if(methodInfo.IsGenericMethodDefinition)
								{
									int paramsCount = methodInfo.GetParameters().Length + (methodInfo.ReturnType == typeof(void) ? 0 : 1) + (methodInfo.IsStatic ? 0 : 1);
									if(action.Value != paramsCount || methodInfo.Name.Contains("GetComponent"))
									{
										continue;
									}
								}

								var id = new MethodIdentifier(this, methodInfo);
								MethodNodeInfo nodeInfo;
								if(methods.TryGetValue(id, out nodeInfo))
								{
									if(nodeInfo.PreAddMethod(type))
									{
										nodeInfo.AddMethod(type, new MethodIdentifier(this, type, methodInfo), module.Key + "." + action.Key);
									}
								}
								else
								{
									methods[id] = new MethodNodeInfo(type, new MethodIdentifier(this, type, methodInfo), module.Key + "." + action.Key);
								}
								continue;
							}
						}
						else if(!(action.Key.StartsWith("__op_") || IsGeneric(action.Key) ||
							action.Key.Contains("SystemGlobalizationCalendar") || //FIX: make a list of actions with missed types (just use the AppendMemberName logic)
							action.Key.Contains("UnityEngineRenderingDefaultReflectionMode") ||
							action.Key.Contains("UnityEngineExperimentalAnimationsMuscleHandle") ||
							action.Key.Contains("SystemIntPtr")))
						{
							UnityEngine.Debug.LogFormat("Unknown action {0} in {1}\n{2}", action.Key, module.Key, string.Join("\n", typeMembers.Keys));
						}
					}
				}
			}

			foreach(var node in UdonEditorManager.Instance.GetNodeDefinitions("Event_"))
			{
				string evtName = GetEventName(node.fullName);

				parameters.Clear();
				var args = node.parameters;
				for(var i = 0; i < args.Count; i++)
				{
					if(args[i].parameterType != UdonNodeParameter.ParameterType.IN)
					{
						string name = args[i].name;
						name = string.Concat(evtName.Substring(1), char.ToUpperInvariant(name[0]), name.Substring(1));
						parameters.Add(new KeyValuePair<string, Type>(name, args[i].type));
					}
				}
				magicMethods.Add(node.name, new MagicMethodInfo(evtName, node.type, parameters.ToArray()));
			}

			this.ctorNames = constructors;
			this.magicMethods = magicMethods;

			var fieldNames = new Dictionary<FieldIdentifier, FieldNameInfo>(fields.Count);
			foreach(var pair in fields) fieldNames.Add(pair.Key, new FieldNameInfo(pair.Value.fullGetName, pair.Value.fullSetName));
			this.fieldNames = fieldNames;

			var methodNames = new Dictionary<MethodIdentifier, string>(methods.Count);
			foreach(var pair in methods) pair.Value.FillNames(methodNames);
			this.methodNames = methodNames;

			var tmpTypesList = new List<Type>();
			var methodBaseTypes = new Dictionary<MethodIdentifier, Type[]>(methods.Count);
			foreach(var pair in methods)
			{
				tmpTypesList.Clear();
				pair.Value.FillTypes(tmpTypesList);
				if(tmpTypesList.Count > 1 || pair.Value.firstIdentifier != pair.Key)
				{
					methodBaseTypes.Add(pair.Key, tmpTypesList.ToArray());
				}
			}
			this.methodBaseTypes = methodBaseTypes;
		}

		private static bool IsGeneric(string str)
		{
			return str.Contains("_T_") || str.EndsWith("_T") || str.Contains("TArray") || str.Contains("ListT") || str.Contains("IEnumerableT");
		}

		private static bool IsSupportedGenericParameter(Type t)
		{
			if(t.IsGenericParameter) return true;
			if(t.IsArray) return t.GetArrayRank() <= 1 && t.GetElementType().IsGenericParameter;
			if(t.IsGenericType)
			{
				var td = t.GetGenericTypeDefinition();
				if(td == typeof(IEnumerable<>) || td == typeof(List<>))
				{
					return t.GetGenericArguments()[0].IsGenericParameter;
				}
			}
			return false;
		}

		private static string GetGenericParameterName(Type t)
		{
			if(t.IsGenericParameter) return t.Name;
			if(t.IsArray) return t.GetElementType().Name + "Array";
			if(t.IsGenericType)
			{
				var name = t.Name;
				return name.Remove(name.LastIndexOf('`')) + t.GetGenericArguments()[0].Name;
			}
			throw new InvalidOperationException();
		}

		private static void CollectTypeMembers(Type type, StringBuilder sb, Dictionary<string, MemberInfo> members, IReadOnlyDictionary<Type, string> supportedTypes)
		{
			if(supportedTypes.TryGetValue(type, out var typeName))
			{
				MemberInfo otherMember;
				String memberName;
				var fields = type.GetFields();
				for(var i = 0; i < fields.Length; i++)
				{
					var field = fields[i];
					if(supportedTypes.TryGetValue(field.FieldType, out var fieldTypeName))
					{
						sb.Clear();
						// sb.Append(typeName);
						// sb.Append('.');

						sb.Append("__get_");
						AppendMemberName(sb, field.Name);

						sb.Append("__");
						sb.Append(fieldTypeName);
						memberName = sb.ToString();
						if(members.TryGetValue(memberName, out otherMember))
						{
							if(otherMember.DeclaringType.IsAssignableFrom(field.DeclaringType))
							{
								members[memberName] = field;
							}
						}
						else members.Add(memberName, field);

						sb.Clear();
						// sb.Append(typeName);
						// sb.Append('.');

						sb.Append("__set_");
						AppendMemberName(sb, field.Name);

						sb.Append("__");
						sb.Append(fieldTypeName);
						memberName = sb.ToString();
						if(members.TryGetValue(memberName, out otherMember))
						{
							if(otherMember.DeclaringType.IsAssignableFrom(field.DeclaringType))
							{
								members[memberName] = field;
							}
						}
						else members.Add(memberName, field);
					}
				}

				var constructors = type.GetConstructors();
				for(var i = 0; i < constructors.Length; i++)
				{
					var constructor = constructors[i];
					sb.Clear();
					// sb.Append(typeName);
					// sb.Append('.');

					sb.Append("__");
					AppendMemberName(sb, constructor.Name);

					sb.Append("__");
					var parameters = constructor.GetParameters();
					if(parameters.Length > 0)
					{
						bool ignore = false;
						for(var j = 0; j < parameters.Length; j++)
						{
							if(supportedTypes.TryGetValue(parameters[j].ParameterType, out var argTypeName))
							{
								if(j > 0) sb.Append('_');
								sb.Append(argTypeName);
							}
							else
							{
								ignore = true;
								break;
							}
						}
						if(ignore) continue;
					}

					sb.Append("__");
					sb.Append(typeName);
					members.Add(sb.ToString(), constructor);
				}

				var methods = type.IsInterface ? GetInterfaceMethods(type) : type.GetMethods();
				for(var i = 0; i < methods.Length; i++)
				{
					var method = methods[i];
					string retTypeName;
					bool supportedReturn;
					if(method.ReturnType == typeof(void))
					{
						supportedReturn = true;
						retTypeName = "SystemVoid";
					}
					else
					{
						supportedReturn = supportedTypes.TryGetValue(method.ReturnType, out retTypeName);
					}
					if(method.IsGenericMethodDefinition)
					{
						if(!supportedReturn)
						{
							supportedReturn = IsSupportedGenericParameter(method.ReturnType);
						}
						if(supportedReturn)
						{
							sb.Clear();
							// sb.Append(typeName);
							// sb.Append('.');

							sb.Append("__");
							AppendMemberName(sb, method.Name);

							var parameters = method.GetParameters();
							if(parameters.Length > 0)
							{
								sb.Append('_');
								bool ignore = false;
								for(var j = 0; j < parameters.Length; j++)
								{
									if(supportedTypes.TryGetValue(parameters[j].ParameterType, out var argTypeName))
									{
										sb.Append('_');
										sb.Append(argTypeName);
									}
									else if(IsSupportedGenericParameter(parameters[j].ParameterType))
									{
										sb.Append('_');
										sb.Append(GetGenericParameterName(parameters[j].ParameterType));
									}
									else
									{
										ignore = true;
										break;
									}
								}
								if(ignore) continue;
							}

							sb.Append("__");
							sb.Append(retTypeName);
							memberName = sb.ToString();
							if(members.TryGetValue(memberName, out otherMember))
							{
								if(otherMember.DeclaringType.IsAssignableFrom(method.DeclaringType))
								{
									members[memberName] = method;
								}
							}
							else members.Add(memberName, method);
						}
					}
					else
					{
						if(supportedReturn)
						{
							sb.Clear();
							// sb.Append(typeName);
							// sb.Append('.');

							sb.Append("__");
							AppendMemberName(sb, method.Name);

							var parameters = method.GetParameters();
							if(parameters.Length > 0)
							{
								sb.Append('_');
								bool ignore = false;
								for(var j = 0; j < parameters.Length; j++)
								{
									if(supportedTypes.TryGetValue(parameters[j].ParameterType, out var argTypeName))
									{
										sb.Append('_');
										sb.Append(argTypeName);
									}
									else
									{
										ignore = true;
										break;
									}
								}
								if(ignore) continue;
							}

							sb.Append("__");
							sb.Append(retTypeName);
							memberName = sb.ToString();
							if(members.TryGetValue(memberName, out otherMember))
							{
								if(otherMember.DeclaringType.IsAssignableFrom(method.DeclaringType))
								{
									members[memberName] = method;
								}
							}
							else members.Add(memberName, method);
						}
					}
				}
			}
		}

		private static MethodInfo[] GetInterfaceMethods(Type type)
		{
			var methods = new HashSet<MethodInfo>();
			var types = new HashSet<Type>();
			CollectInterfaceTypes(type, types);
			foreach(var t in types)
			{
				methods.UnionWith(t.GetMethods());
			}
			var arr = new MethodInfo[methods.Count];
			methods.CopyTo(arr);
			return arr;
		}

		private static void CollectInterfaceTypes(Type type, HashSet<Type> types)
		{
			if(types.Add(type))
			{
				var interfaces = type.GetInterfaces();
				for(var i = 0; i < interfaces.Length; i++)
				{
					CollectInterfaceTypes(interfaces[i], types);
				}
			}
		}

		private static void AppendMemberName(StringBuilder sb, string str)
		{
			var chars = str.ToCharArray();
			for(var i = 0; i < chars.Length; i++)
			{
				var c = chars[i];
				if(char.IsLetter(c)) sb.Append(c);
				if(char.IsDigit(c)) sb.Append(c);
				if(c == '_') sb.Append(c);
			}
		}

		private static string GetEventName(string evt)
		{
			evt = evt.Substring(6);
			return string.Format("_{0}{1}", char.ToLowerInvariant(evt[0]), evt.Substring(1));
		}

		private class MethodNodeInfo
		{
			private MethodNode rootNode = null;
			private MethodNode lastNode = null;

			public MethodIdentifier firstIdentifier => rootNode.identifier;

			public MethodNodeInfo(Type targetType, MethodIdentifier identifier, string fullName)
			{
				this.AddMethod(targetType, identifier, fullName);
			}

			public bool PreAddMethod(Type targetType)
			{
				var node = rootNode;
				while(node != null)
				{
					if(node.targetType == targetType) return false;
					if(node.targetType.IsAssignableFrom(targetType)) return false;
					node = node.next;
				}
				node = rootNode;
				MethodNode prevNode = null;
				while(node != null)
				{
					if(targetType.IsAssignableFrom(node.targetType))
					{
						if(prevNode == null) rootNode = node.next;
						else prevNode.next = node.next;
						if(node.next == null) lastNode = prevNode;
					}
					else prevNode = node;
					node = node.next;
				}
				return true;
			}

			public void AddMethod(Type targetType, MethodIdentifier identifier, string fullName)
			{
				if(rootNode == null)
				{
					lastNode = rootNode = new MethodNode(targetType, identifier, fullName);
				}
				else
				{
					var node = new MethodNode(targetType, identifier, fullName);
					lastNode.next = node;
					lastNode = node;
				}
			}

			public void FillNames(IDictionary<MethodIdentifier, string> list)
			{
				var node = rootNode;
				while(node != null)
				{
					list.Add(node.identifier, node.fullName);
					node = node.next;
				}
			}

			public void FillTypes(IList<Type> list)
			{
				var node = rootNode;
				while(node != null)
				{
					list.Add(node.targetType);
					node = node.next;
				}
			}

			private class MethodNode
			{
				public Type targetType;
				public MethodIdentifier identifier;
				public string fullName;
				public MethodNode next = null;

				public MethodNode(Type targetType, MethodIdentifier identifier, string fullName)
				{
					this.targetType = targetType;
					this.identifier = identifier;
					this.fullName = fullName;
					this.next = null;
				}
			}
		}

		private struct FieldNodeInfo
		{
			public Type declaringType;
			public string fullGetName;
			public string fullSetName;

			public FieldNodeInfo(Type declaringType)
			{
				this.declaringType = declaringType;
				this.fullGetName = null;
				this.fullSetName = null;
			}
		}
	}
}