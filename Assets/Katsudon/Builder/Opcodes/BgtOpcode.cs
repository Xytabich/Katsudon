using System.Reflection.Emit;
using Katsudon.Builder.Externs;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class BgtOpcode : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			bool unsigned = method.currentOp.opCode == OpCodes.Bgt_Un || method.currentOp.opCode == OpCodes.Bgt_Un_S;
			int methodAddress = (int)method.currentOp.argument;

			var b = method.PopStack();
			var a = method.PopStack();

			IVariable variable = null;
			CleOpcode.ProcessOp(method, unsigned, a, b, () => (variable = method.GetTmpVariable(typeof(bool))), out variable);
			method.machine.AddBranch(variable, method.GetMachineAddressLabel(methodAddress));
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new BgtOpcode();
			container.RegisterOpBuilder(OpCodes.Bgt, builder);
			container.RegisterOpBuilder(OpCodes.Bgt_S, builder);
			container.RegisterOpBuilder(OpCodes.Bgt_Un, builder);
			container.RegisterOpBuilder(OpCodes.Bgt_Un_S, builder);
		}
	}
}