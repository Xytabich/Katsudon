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
			container.RegisterOpBuilder(OpCodes.Break, builder);
			container.RegisterOpBuilder(OpCodes.Tailcall, builder);
			container.RegisterOpBuilder(OpCodes.Constrained, builder);
			container.RegisterOpBuilder(OpCodes.Readonly, builder);
			container.RegisterOpBuilder(OpCodes.Unaligned, builder);
			container.RegisterOpBuilder(OpCodes.Volatile, builder);
		}
	}
}