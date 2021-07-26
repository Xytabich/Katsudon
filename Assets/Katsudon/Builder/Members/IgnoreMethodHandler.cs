using System.Diagnostics;
using System.Reflection;
using Katsudon.Builder;
using Katsudon.Info;

namespace Katsudon.Members
{
	[MemberHandler]
	public class IgnoreMethodHandler : IMemberHandler
	{
		int IMemberHandler.order => 10;

		bool IMemberHandler.Process(MemberInfo member, AssembliesInfo assemblies, AsmTypeInfo typeInfo)
		{
			var methodInfo = member as MethodInfo;
			var condition = methodInfo.GetCustomAttribute<ConditionalAttribute>();
			return condition != null && condition.ConditionString == "UNITY_EDITOR";
		}

		public static void Register(IMemberHandlersRegistry registry)
		{
			registry.RegisterHandler(MemberTypes.Method, new IgnoreMethodHandler());
		}
	}
}