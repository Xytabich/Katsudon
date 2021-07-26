using System.Reflection.Emit;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class LdlocOpcode : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			int locIndex;
			if(ILUtils.TryGetLdloc(method.currentOp, out locIndex))
			{
				method.PushStack(method.GetLocalVariable(locIndex));
			}
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new LdlocOpcode();
			container.RegisterOpBuilder(OpCodes.Ldloc_0, builder);
			container.RegisterOpBuilder(OpCodes.Ldloc_1, builder);
			container.RegisterOpBuilder(OpCodes.Ldloc_2, builder);
			container.RegisterOpBuilder(OpCodes.Ldloc_3, builder);
			container.RegisterOpBuilder(OpCodes.Ldloc, builder);
			container.RegisterOpBuilder(OpCodes.Ldloc_S, builder);
			container.RegisterOpBuilder(OpCodes.Ldloca, builder);
			container.RegisterOpBuilder(OpCodes.Ldloca_S, builder);
		}
	}
}