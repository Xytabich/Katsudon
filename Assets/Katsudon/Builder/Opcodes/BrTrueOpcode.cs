using System.Reflection.Emit;

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
			var skipLabel = new EmbedAddressLabel();
			udonMachine.AddBranch(condition, skipLabel);
			udonMachine.AddJump(method.GetMachineAddressLabel(methodAddress));
			udonMachine.ApplyLabel(skipLabel);
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