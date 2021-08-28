using System.Reflection;
using System.Reflection.Emit;

namespace Katsudon.Builder.Extensions.UdonExtensions
{
	[OperationBuilder]
	public class SendCustomNetworkEventCall : IOperationBuider
	{
		public int order => 15;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = method.currentOp.argument as MethodInfo;
			if(methodInfo.Name == nameof(AbstractCallsHelper.SendCustomNetworkEvent) && methodInfo.DeclaringType == typeof(AbstractCallsHelper))
			{
				var eventName = method.PopStack();
				var target = method.PopStack();
				method.machine.AddExtern("VRCUdonCommonInterfacesIUdonEventReceiver.__SendCustomNetworkEvent__VRCUdonCommonInterfacesNetworkEventTarget_SystemString__SystemVoid",
					target.OwnType(), eventName.OwnType());
				return true;
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new SendCustomNetworkEventCall();
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
		}
	}
}