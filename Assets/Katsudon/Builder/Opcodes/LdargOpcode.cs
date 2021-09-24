using System.Reflection.Emit;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class LdargOpcode : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			int argIndex;
			if(ILUtils.TryGetLdarg(method.currentOp, out argIndex, !method.isStatic))
			{
				if(argIndex < 0) method.PushStack(method.machine.GetThisVariable());
				else method.PushStack(method.GetArgumentVariable(argIndex));
			}
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new LdargOpcode();
			container.RegisterOpBuilder(OpCodes.Ldarg_0, builder);
			container.RegisterOpBuilder(OpCodes.Ldarg_1, builder);
			container.RegisterOpBuilder(OpCodes.Ldarg_2, builder);
			container.RegisterOpBuilder(OpCodes.Ldarg_3, builder);
			container.RegisterOpBuilder(OpCodes.Ldarg, builder);
			container.RegisterOpBuilder(OpCodes.Ldarg_S, builder);
			container.RegisterOpBuilder(OpCodes.Ldarga, builder);
			container.RegisterOpBuilder(OpCodes.Ldarga_S, builder);
		}
	}
}