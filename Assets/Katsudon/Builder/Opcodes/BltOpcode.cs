using System.Reflection.Emit;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class BltOpcode : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			bool unsigned = method.currentOp.opCode == OpCodes.Blt_Un || method.currentOp.opCode == OpCodes.Blt_Un_S;
			int methodAddress = (int)method.currentOp.argument;

			var b = method.PopStack();
			var a = method.PopStack();

			IVariable variable = null;
			CgeOpcode.ProcessOp(method, unsigned, a, b, () => (variable = method.GetTmpVariable(typeof(bool))), out variable);
			var handle = new StoreBranchingStackHandle(method, methodAddress);
			method.machine.AddBranch(variable, method.GetMachineAddressLabel(methodAddress));
			handle.Dispose();
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new BltOpcode();
			container.RegisterOpBuilder(OpCodes.Blt, builder);
			container.RegisterOpBuilder(OpCodes.Blt_S, builder);
			container.RegisterOpBuilder(OpCodes.Blt_Un, builder);
			container.RegisterOpBuilder(OpCodes.Blt_Un_S, builder);
		}
	}
}