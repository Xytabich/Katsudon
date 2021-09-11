using System.Reflection;
using System.Reflection.Emit;

namespace Katsudon.Builder.Extensions.UdonExtensions
{
	[OperationBuilder]
	public class DisableInteractivePropertyCall : IOperationBuider
	{
		public int order => 15;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = method.currentOp.argument as MethodInfo;
			if(methodInfo.Name == nameof(AbstractCallsHelper.DisableInteractive) && methodInfo.DeclaringType == typeof(AbstractCallsHelper))
			{
				if(methodInfo.ReturnType == typeof(void))
				{
					var value = method.PopStack();
					var target = method.PopStack();
					method.machine.AddExtern("VRCUdonCommonInterfacesIUdonEventReceiver.__set_DisableInteractive__SystemBoolean__SystemVoid",
						target.OwnType(), value.UseType(typeof(bool))
					);
				}
				else
				{
					var target = method.PopStack();
					method.machine.AddExtern("VRCUdonCommonInterfacesIUdonEventReceiver.__get_DisableInteractive__SystemBoolean",
						() => method.GetOrPushOutVariable(typeof(bool)),
						target.OwnType()
					);
				}
				return true;
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new DisableInteractivePropertyCall();
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
		}
	}
}