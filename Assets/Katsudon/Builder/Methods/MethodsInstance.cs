using System.Collections.Generic;
using System.Reflection;
using Katsudon.Builder.Helpers;
using Katsudon.Info;

namespace Katsudon.Builder.Methods
{
	public class MethodsInstance
	{
		private Dictionary<MethodIdentifier, UBehMethodInfo> methods = new Dictionary<MethodIdentifier, UBehMethodInfo>();
		private AsmTypeInfo typeInfo;

		public MethodsInstance(AsmTypeInfo typeInfo, List<MethodInfo> buildOrder, HashSet<MethodInfo> buildMethods)
		{
			this.typeInfo = typeInfo;
		}

		public UBehMethodInfo GetDirect(MethodInfo method)
		{
			var id = UdonCacheHelper.cache.GetMethodIdentifier(method);
			if(methods.TryGetValue(id, out var info)) return info;

			return CreateMethod(id, typeInfo.GetMethod(method));
		}

		public UBehMethodInfo GetFamily(MethodInfo method)
		{
			var id = UdonCacheHelper.cache.GetMethodIdentifier(method);
			if(methods.TryGetValue(id, out var info)) return info;

			return CreateMethod(id, typeInfo.GetFamilyMethod(method));
		}

		public UBehMethodInfo GetByInfo(AsmMethodInfo nameInfo)
		{
			if(nameInfo == null) return null;

			var id = UdonCacheHelper.cache.GetMethodIdentifier(nameInfo.method);
			if(methods.TryGetValue(id, out var info)) return info;

			return CreateMethod(id, nameInfo);
		}

		private UBehMethodInfo CreateMethod(MethodIdentifier id, AsmMethodInfo nameInfo)
		{
			if(nameInfo == null) return null;

			bool export = (nameInfo.flags & AsmMethodInfo.Flags.Export) != 0;
			string name = nameInfo.name;

			var method = nameInfo.method;
			var parameters = method.GetParameters();
			IVariable[] args = new IVariable[parameters.Length];
			IVariable ret = null;
			var argNames = nameInfo.parametersName;
			for(var i = 0; i < parameters.Length; i++)
			{
				var type = parameters[i].ParameterType;
				args[i] = new NamedVariable(argNames[i], type.IsByRef ? type.GetElementType() : type);
			}
			if(method.ReturnType != typeof(void))
			{
				ret = new NamedVariable(nameInfo.returnName, method.ReturnType);
			}

			var info = new UBehMethodInfo(name, export, args, ret);
			methods[id] = info;
			return info;
		}
	}
}