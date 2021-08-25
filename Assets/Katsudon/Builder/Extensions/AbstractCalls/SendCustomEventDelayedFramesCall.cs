using System.Reflection;
using System.Reflection.Emit;
using VRC.Udon.Common.Enums;

namespace Katsudon.Builder.Extensions.AbstractCalls
{
	[OperationBuilder]
	public class SendCustomEventDelayedFramesCall : IOperationBuider
	{
		public int order => 15;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = method.currentOp.argument as MethodInfo;
			if(methodInfo.Name == nameof(AbstractCallsHelper.SendCustomEventDelayedFrames) && methodInfo.DeclaringType == typeof(AbstractCallsHelper))
			{
				var timing = method.PopStack();
				var frames = method.PopStack();
				var eventName = method.PopStack();
				var target = method.PopStack();
				method.machine.AddExtern("VRCUdonCommonInterfacesIUdonEventReceiver.__SendCustomEventDelayedFrames__SystemString_SystemInt32_VRCUdonCommonEnumsEventTiming__SystemVoid",
					target.OwnType(), eventName.OwnType(), frames.UseType(typeof(int)), timing.UseType(typeof(EventTiming)));
				return true;
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new SendCustomEventDelayedFramesCall();
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
		}
	}
}