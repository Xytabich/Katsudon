using System.Reflection.Emit;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class StargOpcode : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			int argIndex;
			if(ILUtils.TryGetStarg(method.currentOp, out argIndex))
			{
				if(!method.isStatic) argIndex--;
				var arg = method.GetArgumentVariable(argIndex);
				method.machine.AddCopy(method.PopStack(), arg, arg.type);
			}
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new StargOpcode();
			container.RegisterOpBuilder(OpCodes.Starg, builder);
			container.RegisterOpBuilder(OpCodes.Starg_S, builder);
		}
	}
}