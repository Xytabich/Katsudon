using System;
using System.Collections.Generic;
using System.Reflection;
using Katsudon.Info;

namespace Katsudon.Members
{
	/// <summary>
	/// Drops explicitly defined interface methods (they will be added separately)
	/// </summary>
	[MemberHandler]
	public class InterfaceMethodHandler : IMemberHandler
	{
		int IMemberHandler.order => 10;

		private Dictionary<Type, HashSet<MethodInfo>> methods = new Dictionary<Type, HashSet<MethodInfo>>();

		bool IMemberHandler.Process(MemberInfo member, AssembliesInfo assemblies, AsmTypeInfo typeInfo)
		{
			var method = member as MethodInfo;
			if(method.IsStatic || method.IsPublic) return false;
			return ContainsMethod(method);
		}

		private bool ContainsMethod(MethodInfo methodInfo)
		{
			var type = methodInfo.DeclaringType;
			HashSet<MethodInfo> list;
			if(!methods.TryGetValue(type, out list))
			{
				list = null;
				var interfaces = type.GetInterfaces();
				for(var i = 0; i < interfaces.Length; i++)
				{
					var map = type.GetInterfaceMap(interfaces[i]);
					var typeMethods = map.TargetMethods;
					for(var j = 0; j < typeMethods.Length; j++)
					{
						if(!typeMethods[j].IsPublic)
						{
							if(list == null) list = new HashSet<MethodInfo>();
							list.Add(typeMethods[j]);
						}
					}
				}
				methods[type] = list;
			}
			if(list == null) return false;
			return list.Contains(methodInfo);
		}

		public static void Register(IMemberHandlersRegistry registry)
		{
			registry.RegisterHandler(MemberTypes.Method, new InterfaceMethodHandler());
		}
	}
}