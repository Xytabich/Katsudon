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
			var condition = method.PopStack();
			var handle = new StoreBranchingStackHandle(method, methodAddress);
			method.machine.AddBranch(condition, method.GetMachineAddressLabel(methodAddress));
			handle.Dispose();
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