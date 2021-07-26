using System.Reflection.Emit;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class BrOpcode : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			int methodAddress = (int)method.currentOp.argument;
			method.machine.AddJump(method.GetMachineAddressLabel(methodAddress));
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new BrOpcode();
			container.RegisterOpBuilder(OpCodes.Br, builder);
			container.RegisterOpBuilder(OpCodes.Br_S, builder);
		}
	}
}