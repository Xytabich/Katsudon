using System.Reflection;
using Katsudon.Builder;
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
			typeInfo.AddMethod(new AsmMethodInfo(AsmMethodInfo.Flags.Export | AsmMethodInfo.Flags.Family,
				Utils.PrepareMethodName(method), method));
			return true;
		}

		public static void Register(IMemberHandlersRegistry registry)
		{
			registry.RegisterHandler(MemberTypes.Method, new MethodsCollector());
		}
	}
}