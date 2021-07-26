using System.Reflection.Emit;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder(1)]
	public class BeqOpcode : IOperationBuider
	{
		public int order => 0;

		private BrTrueOpcode brTrue;

		private BeqOpcode(BrTrueOpcode brTrue)
		{
			this.brTrue = brTrue;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			int methodAddress = (int)method.currentOp.argument;

			var b = method.PopStack();
			var a = method.PopStack();

			IVariable variable = null;
			CeqOpcode.ProcessOp(method, a, b, () => (variable = method.GetTmpVariable(typeof(bool))), out variable);
			brTrue.ProcessOp(method, method.machine, methodAddress, variable);
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new BeqOpcode(modules.GetModule<BrTrueOpcode>());
			container.RegisterOpBuilder(OpCodes.Beq, builder);
			container.RegisterOpBuilder(OpCodes.Beq_S, builder);
		}
	}
}