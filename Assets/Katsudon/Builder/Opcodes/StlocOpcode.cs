using System.Reflection.Emit;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class StlocOpcode : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			int locIndex;
			if(ILUtils.TryGetStloc(method.currentOp, out locIndex))
			{
				var variable = method.PopStack();
				var local = method.GetLocalVariable(locIndex);
				method.machine.AddCopy(variable, local, local.type);
			}
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new StlocOpcode();
			container.RegisterOpBuilder(OpCodes.Stloc, builder);
			container.RegisterOpBuilder(OpCodes.Stloc_0, builder);
			container.RegisterOpBuilder(OpCodes.Stloc_1, builder);
			container.RegisterOpBuilder(OpCodes.Stloc_2, builder);
			container.RegisterOpBuilder(OpCodes.Stloc_3, builder);
			container.RegisterOpBuilder(OpCodes.Stloc_S, builder);
		}
	}
}