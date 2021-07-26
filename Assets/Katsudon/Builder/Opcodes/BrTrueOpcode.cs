using System.Reflection.Emit;
using Katsudon.Builder.Externs;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class BrTrueOpcode : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			int methodAddress = (int)method.currentOp.argument;
			ProcessOp(method, method.machine, methodAddress, method.PopStack());
			return true;
		}

		public void ProcessOp(IMethodDescriptor method, IUdonMachine udonMachine, int methodAddress, IVariable condition)
		{
			IVariable outVariable = null;
			udonMachine.UnaryOperatorExtern(UnaryOperator.UnaryNegation, condition.UseType(typeof(bool)), typeof(bool), () => (outVariable = method.GetTmpVariable(typeof(bool))));
			udonMachine.AddBranch(outVariable, method.GetMachineAddressLabel(methodAddress));
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new BrTrueOpcode();
			container.RegisterOpBuilder(OpCodes.Brtrue, builder);
			container.RegisterOpBuilder(OpCodes.Brtrue_S, builder);
			modules.AddModule(builder);
		}
	}
}