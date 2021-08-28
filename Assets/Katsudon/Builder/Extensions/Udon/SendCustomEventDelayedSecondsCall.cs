using System.Reflection;
using System.Reflection.Emit;
using VRC.Udon.Common.Enums;

namespace Katsudon.Builder.Extensions.UdonExtensions
{
	[OperationBuilder]
	public class SendCustomEventDelayedSecondsCall : IOperationBuider
	{
		public int order => 15;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = method.currentOp.argument as MethodInfo;
			if(methodInfo.Name == nameof(AbstractCallsHelper.SendCustomEventDelayedSeconds) && methodInfo.DeclaringType == typeof(AbstractCallsHelper))
			{
				var timing = method.PopStack();
				var seconds = method.PopStack();
				var eventName = method.PopStack();
				var target = method.PopStack();
				method.machine.AddExtern("VRCUdonCommonInterfacesIUdonEventReceiver.__SendCustomEventDelayedSeconds__SystemString_SystemSingle_VRCUdonCommonEnumsEventTiming__SystemVoid",
					target.OwnType(), eventName.OwnType(), seconds.OwnType(), timing.UseType(typeof(EventTiming)));
				return true;
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new SendCustomEventDelayedSecondsCall();
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
		}
	}
}