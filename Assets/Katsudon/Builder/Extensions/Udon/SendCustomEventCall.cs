using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;

namespace Katsudon.Builder.Extensions.UdonExtensions
{
	[OperationBuilder]
	public class SendCustomEventCall : IOperationBuider
	{
		public int order => 15;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = method.currentOp.argument as MethodInfo;
			if(methodInfo.Name == nameof(AbstractCallsHelper.SendCustomEvent) && methodInfo.DeclaringType == typeof(AbstractCallsHelper))
			{
				var eventName = method.PopStack();
				var target = method.PopStack();
				method.machine.SendEventExtern(target, eventName);
				return true;
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new SendCustomEventCall();
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
		}
	}
}