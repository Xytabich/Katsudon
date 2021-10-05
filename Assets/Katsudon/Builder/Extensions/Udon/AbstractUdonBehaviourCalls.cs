using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;
using VRC.SDK3.Components;
using VRC.Udon.Common.Enums;
using VRC.Udon.Common.Interfaces;

namespace Katsudon.Builder.Extensions.UdonExtensions
{
	[OperationBuilder]
	public class AbstractUdonBehaviourCalls : IOperationBuider
	{
		private delegate bool MethodBuilder(IMethodDescriptor descriptor, MethodInfo method);

		public int order => 20;

		private Dictionary<string, MethodBuilder> builders = new Dictionary<string, MethodBuilder>() {
			{ "GetProgramVariable", GetProgramVariableBuilder},
			{ "GetProgramVariableType", GetProgramVariableTypeBuilder},
			{ "SetProgramVariable", SetProgramVariableBuilder},
			{ "SendCustomEvent", SendCustomEventBuilder},
			{ "SendCustomEventDelayedFrames", SendCustomEventDelayedFramesBuilder},
			{ "SendCustomEventDelayedSeconds", SendCustomEventDelayedSecondsBuilder},
			{ "SendCustomNetworkEvent", SendCustomNetworkEventBuilder},
			{ "RequestSerialization", RequestSerializationBuilder},
			{ "get_DisableInteractive", DisableInteractiveGetterBuilder},
			{ "set_DisableInteractive", DisableInteractiveSetterBuilder}
		};

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = method.currentOp.argument as MethodInfo;
			if(methodInfo.DeclaringType == typeof(AbstractUdonBehaviour))
			{
				if(builders.TryGetValue(methodInfo.Name, out var builder))
				{
					return builder.Invoke(method, methodInfo);
				}
			}
			return false;
		}

		private static bool GetProgramVariableBuilder(IMethodDescriptor descriptor, MethodInfo method)
		{
			var name = descriptor.PopStack();
			var target = descriptor.PopStack();
			descriptor.machine.GetVariableExtern(target, name, () => descriptor.GetOrPushOutVariable(method.ReturnType));
			return true;
		}

		private static bool GetProgramVariableTypeBuilder(IMethodDescriptor descriptor, MethodInfo method)
		{
			var name = descriptor.PopStack();
			var target = descriptor.PopStack();
			descriptor.machine.GetVariableTypeExtern(target, name, () => descriptor.GetOrPushOutVariable(method.ReturnType));
			return true;
		}

		private static bool SetProgramVariableBuilder(IMethodDescriptor descriptor, MethodInfo method)
		{
			var value = descriptor.PopStack();
			var name = descriptor.PopStack();
			var target = descriptor.PopStack();
			descriptor.machine.SetVariableExtern(target, name, value.UseType(method.GetParameters()[0].ParameterType));
			return true;
		}

		private static bool SendCustomEventBuilder(IMethodDescriptor descriptor, MethodInfo method)
		{
			var name = descriptor.PopStack();
			var target = descriptor.PopStack();
			descriptor.machine.SendEventExtern(target, name);
			return true;
		}

		private static bool SendCustomEventDelayedFramesBuilder(IMethodDescriptor descriptor, MethodInfo method)
		{
			var timing = descriptor.PopStack();
			var frames = descriptor.PopStack();
			var eventName = descriptor.PopStack();
			var target = descriptor.PopStack();
			descriptor.machine.AddExtern("VRCUdonCommonInterfacesIUdonEventReceiver.__SendCustomEventDelayedFrames__SystemString_SystemInt32_VRCUdonCommonEnumsEventTiming__SystemVoid",
				target.OwnType(), eventName.OwnType(), frames.UseType(typeof(int)), timing.UseType(typeof(EventTiming)));
			return true;
		}

		private static bool SendCustomEventDelayedSecondsBuilder(IMethodDescriptor descriptor, MethodInfo method)
		{
			var timing = descriptor.PopStack();
			var seconds = descriptor.PopStack();
			var eventName = descriptor.PopStack();
			var target = descriptor.PopStack();
			descriptor.machine.AddExtern("VRCUdonCommonInterfacesIUdonEventReceiver.__SendCustomEventDelayedSeconds__SystemString_SystemSingle_VRCUdonCommonEnumsEventTiming__SystemVoid",
				target.OwnType(), eventName.OwnType(), seconds.OwnType(), timing.UseType(typeof(EventTiming)));
			return true;
		}

		private static bool SendCustomNetworkEventBuilder(IMethodDescriptor descriptor, MethodInfo method)
		{
			var eventName = descriptor.PopStack();
			var eventTarget = descriptor.PopStack();
			var target = descriptor.PopStack();
			descriptor.machine.AddExtern("VRCUdonCommonInterfacesIUdonEventReceiver.__SendCustomNetworkEvent__VRCUdonCommonInterfacesNetworkEventTarget_SystemString__SystemVoid",
				target.OwnType(), eventTarget.UseType(typeof(NetworkEventTarget)), eventName.OwnType());
			return true;
		}

		private static bool RequestSerializationBuilder(IMethodDescriptor descriptor, MethodInfo method)
		{
			var target = descriptor.PopStack();
			descriptor.machine.AddExtern("VRCUdonCommonInterfacesIUdonEventReceiver.__RequestSerialization__SystemVoid", target.OwnType());
			return true;
		}

		private static bool DisableInteractiveGetterBuilder(IMethodDescriptor descriptor, MethodInfo method)
		{
			var target = descriptor.PopStack();
			descriptor.machine.AddExtern("VRCUdonCommonInterfacesIUdonEventReceiver.__get_DisableInteractive__SystemBoolean",
				() => descriptor.GetOrPushOutVariable(typeof(bool)),
				target.OwnType()
			);
			return true;
		}

		private static bool DisableInteractiveSetterBuilder(IMethodDescriptor descriptor, MethodInfo method)
		{
			var value = descriptor.PopStack();
			var target = descriptor.PopStack();
			descriptor.machine.AddExtern("VRCUdonCommonInterfacesIUdonEventReceiver.__set_DisableInteractive__SystemBoolean__SystemVoid",
				target.OwnType(), value.UseType(typeof(bool))
			);
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new AbstractUdonBehaviourCalls();
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
		}
	}
}