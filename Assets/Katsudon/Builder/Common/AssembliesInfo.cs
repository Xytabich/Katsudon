using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Katsudon.Members;
using UnityEngine;

namespace Katsudon.Info
{
	public class AssembliesInfo
	{
		private static AssembliesInfo _instance;
		public static AssembliesInfo instance => _instance ?? (_instance = new AssembliesInfo());

		public MembersProcessor processor { get; private set; }

		private Dictionary<Type, AsmTypeInfo> types = new Dictionary<Type, AsmTypeInfo>();

		private AssembliesInfo()
		{
			processor = new MembersProcessor(this);
		}

		public AsmTypeInfo GetTypeInfo(Type type)
		{
			AsmTypeInfo info;
			if(types.TryGetValue(type, out info)) return info;
			if(!Utils.IsUdonAsm(type))
			{
				throw new Exception(string.Format("Type {0} is not supported because it is not contained in an assembly marked with the UdonAsm attribute.", type));
			}

			if(type.IsInterface)
			{
				info = BuildInterfaceInfo(type);
			}
			else if(type.IsClass && typeof(MonoBehaviour).IsAssignableFrom(type))
			{
				info = BuildBehaviourInfo(type);
			}
			else
			{
				throw new Exception(string.Format("Type {0} is not supported.", type));
			}
			types[type] = info;
			return info;
		}

		public AsmFieldInfo GetField(Type targetType, FieldInfo field)
		{
			if(!typeof(MonoBehaviour).IsAssignableFrom(targetType) || !targetType.Assembly.IsDefined(typeof(UdonAsmAttribute), false))
			{
				return null;
			}
			return GetTypeInfo(targetType).GetField(field);
		}

		public AsmMethodInfo GetMethod(Type targetType, MethodInfo method)
		{
			if(!(targetType.IsInterface || typeof(MonoBehaviour).IsAssignableFrom(targetType)) ||
				!targetType.Assembly.IsDefined(typeof(UdonAsmAttribute), false))
			{
				return null;
			}
			return GetTypeInfo(targetType).GetMethod(method);
		}

		private AsmTypeInfo BuildInterfaceInfo(Type type)
		{
			var inherits = new HashSet<AsmTypeInfo>();
			foreach(var interfaceType in type.GetInterfaces())
			{
				inherits.UnionWith(GetTypeInfo(interfaceType).GetInheritance());
			}

			var guid = Guid.NewGuid();
			var types = inherits.ToArray();
			var info = new AsmTypeInfo(type, guid, types, types);
			var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
			for(var i = 0; i < methods.Length; i++)
			{
				info.AddMethod(new AsmMethodInfo(AsmMethodInfo.Flags.Export | AsmMethodInfo.Flags.Unique,
					Utils.PrepareInterfaceMethodName(methods[i]), methods[i]));
			}
			return info;
		}

		private AsmTypeInfo BuildBehaviourInfo(Type type)
		{
			var hierarhy = new List<AsmTypeInfo>();
			var inherits = new HashSet<AsmTypeInfo>();
			foreach(var interfaceType in type.GetInterfaces())
			{
				inherits.UnionWith(GetTypeInfo(interfaceType).GetInheritance());
			}

			if(type.BaseType != typeof(MonoBehaviour))
			{
				var baseInfo = GetTypeInfo(type.BaseType);
				inherits.UnionWith(baseInfo.GetInheritance());

				hierarhy.AddRange(baseInfo.GetClassHierarhy());
				hierarhy.Add(baseInfo);
			}

			var guid = Guid.NewGuid();
			var info = new AsmTypeInfo(type, guid, inherits.ToArray(), hierarhy.ToArray());
			processor.ProcessMembers(type, info);
			return info;
		}
	}

	public sealed class AsmMethodInfo
	{
		public readonly Flags flags;
		public readonly string name;
		public readonly string[] parametersName;
		public readonly string returnName = null;

		public readonly MethodInfo method;

		public AsmMethodInfo(Flags flags, string name, MethodInfo method)
		{
			this.name = name;
			this.flags = flags;
			this.method = method;
			var parameters = method.GetParameters();
			this.parametersName = new string[parameters.Length];
			for(var i = 0; i < parameters.Length; i++)
			{
				this.parametersName[i] = string.Format("{0}__{1}", name, Utils.PrepareMemberName(parameters[i].Name));
			}
			if(method.ReturnType != typeof(void))
			{
				this.returnName = string.Format("{0}__return", name);
			}
		}

		public AsmMethodInfo(Flags flags, string name, string[] parameters, string ret, MethodInfo method)
		{
			this.name = name;
			this.flags = flags;
			this.parametersName = parameters;
			this.returnName = ret;
			this.method = method;
		}

		public AsmMethodInfo(string name, string[] parameters, string ret, AsmMethodInfo method)
		{
			this.name = name;
			this.parametersName = parameters;
			this.returnName = ret;
			this.flags = method.flags;
			this.method = method.method;
		}

		public AsmMethodInfo(Flags flags, AsmMethodInfo info, MethodInfo method)
		{
			this.flags = flags;
			this.name = info.name;
			this.parametersName = info.parametersName;
			this.returnName = info.returnName;
			this.method = method;
		}

		public enum Flags
		{
			None,
			Export = 0x01,
			Network = 0x02,
			/// <summary>
			/// Unique methods can only use their own name; therefore, there cannot be more than one method with this name in one declaring type.
			/// Moreover, if there is a method with this name in the base class, it will become private.
			/// </summary>
			Unique = 0x04,
			/// <summary>
			/// Family methods can override methods with the same structure from base types, i.e. use method and parameter names.
			/// </summary>
			Family = 0x08
		}
	}

	public sealed class AsmFieldInfo
	{
		public readonly Flags flags;
		public readonly string name;
		public readonly SyncMode syncMode = SyncMode.NotSynced;

		public readonly FieldInfo field;

		public AsmFieldInfo(Flags flags, SyncMode syncMode, string name, FieldInfo field)
		{
			this.name = name;
			this.flags = flags;
			this.syncMode = syncMode;
			this.field = field;
		}

		public AsmFieldInfo(string name, AsmFieldInfo field)
		{
			this.name = name;
			this.flags = field.flags;
			this.syncMode = field.syncMode;
			this.field = field.field;
		}

		public enum Flags
		{
			None,
			Export = 0x01,
			Sync = 0x02,
			/// <summary>
			/// Unique fields can only use their own name; therefore, the entire class cannot have more than one field with this name.
			/// </summary>
			Unique = 0x04
		}
	}
}