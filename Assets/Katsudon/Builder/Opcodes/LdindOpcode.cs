using System.Reflection.Emit;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class LdindOpcode : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new LdindOpcode();
			container.RegisterOpBuilder(OpCodes.Ldind_I1, builder);
			container.RegisterOpBuilder(OpCodes.Ldind_I2, builder);
			container.RegisterOpBuilder(OpCodes.Ldind_I4, builder);
			container.RegisterOpBuilder(OpCodes.Ldind_I, builder);
			container.RegisterOpBuilder(OpCodes.Ldind_I8, builder);
			container.RegisterOpBuilder(OpCodes.Ldind_U1, builder);
			container.RegisterOpBuilder(OpCodes.Ldind_U2, builder);
			container.RegisterOpBuilder(OpCodes.Ldind_U4, builder);
			container.RegisterOpBuilder(OpCodes.Ldind_R4, builder);
			container.RegisterOpBuilder(OpCodes.Ldind_R8, builder);
			container.RegisterOpBuilder(OpCodes.Ldind_Ref, builder);
		}
	}
}