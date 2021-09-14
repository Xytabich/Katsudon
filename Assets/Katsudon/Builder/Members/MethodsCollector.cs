using System;
using System.Reflection;
using Katsudon.Info;

namespace Katsudon.Members
{
	[MemberHandler]
	public class MethodsCollector : IMemberHandler
	{
		int IMemberHandler.order => 30;

		bool IMemberHandler.Process(MemberInfo member, AssembliesInfo assemblies, AsmTypeInfo typeInfo)
		{
			var method = member as MethodInfo;
			if(method.IsStatic) return false;
			MethodsCollector.CheckRefParameters(method);
			typeInfo.AddMethod(new AsmMethodInfo(AsmMethodInfo.Flags.Export | AsmMethodInfo.Flags.Family,
				Utils.PrepareMethodName(method), method));
			return true;
		}

		public static void Register(IMemberHandlersRegistry registry)
		{
			registry.RegisterHandler(MemberTypes.Method, new MethodsCollector());
		}

		public static void CheckRefParameters(MethodInfo method)
		{
			if((method.MethodImplementationFlags & MethodImplAttributes.AggressiveInlining) != 0)
			{
				if(method.IsPrivate) return;
			}
			bool hasByRef = method.ReturnType.IsByRef;
			if(!hasByRef)
			{
				var parameters = method.GetParameters();
				for(int i = 0; i < parameters.Length; i++)
				{
					if(parameters[i].ParameterType.IsByRef)
					{
						hasByRef = true;
						break;
					}
				}
			}
			if(hasByRef) throw new Exception(string.Format("Reference variables are not currently supported: {0}:{1}", method.DeclaringType, method));
		}
	}
}