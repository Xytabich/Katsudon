using System.Reflection.Emit;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class IgnoreOpcodes : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new IgnoreOpcodes();
			container.RegisterOpBuilder(OpCodes.Nop, builder);
			container.RegisterOpBuilder(OpCodes.Unbox, builder);
			container.RegisterOpBuilder(OpCodes.Unbox_Any, builder);
			container.RegisterOpBuilder(OpCodes.Castclass, builder);
			container.RegisterOpBuilder(OpCodes.Tailcall, builder);
			container.RegisterOpBuilder(OpCodes.Constrained, builder);
			container.RegisterOpBuilder(OpCodes.Readonly, builder);
			container.RegisterOpBuilder(OpCodes.Unaligned, builder);
			container.RegisterOpBuilder(OpCodes.Volatile, builder);
		}
	}
}