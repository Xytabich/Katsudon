using System.Reflection;
using Katsudon.Info;

namespace Katsudon.Members
{
	[MemberHandler]
	public class GenericMethodHandler : IMemberHandler
	{
		int IMemberHandler.order => -10;

		bool IMemberHandler.Process(MemberInfo member, AssembliesInfo assemblies, AsmTypeInfo typeInfo)
		{
			var method = member as MethodInfo;
			if(method.IsGenericMethod) throw new System.Exception("Generic methods is not supported now");
			return false;
		}

		public static void Register(IMemberHandlersRegistry registry)
		{
			registry.RegisterHandler(MemberTypes.Method, new GenericMethodHandler());
		}
	}
}