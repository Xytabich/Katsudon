using System.Reflection.Emit;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class BrFalseOpcode : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			int methodAddress = (int)method.currentOp.argument;
			method.machine.AddBranch(method.PopStack(), method.GetMachineAddressLabel(methodAddress));
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new BrFalseOpcode();
			container.RegisterOpBuilder(OpCodes.Brfalse, builder);
			container.RegisterOpBuilder(OpCodes.Brfalse_S, builder);
		}
	}
}