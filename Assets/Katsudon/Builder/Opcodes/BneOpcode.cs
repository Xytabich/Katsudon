using System.Reflection.Emit;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class BneOpcode : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			int methodAddress = (int)method.currentOp.argument;

			var b = method.PopStack();
			var a = method.PopStack();

			IVariable variable = null;
			CeqOpcode.ProcessOp(method, a, b, () => (variable = method.GetTmpVariable(typeof(bool))), out variable);
			var handle = new StoreBranchingStackHandle(method, methodAddress);
			method.machine.AddBranch(variable, method.GetMachineAddressLabel(methodAddress));
			handle.Dispose();
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new BneOpcode();
			container.RegisterOpBuilder(OpCodes.Bne_Un, builder);
			container.RegisterOpBuilder(OpCodes.Bne_Un_S, builder);
		}
	}
}