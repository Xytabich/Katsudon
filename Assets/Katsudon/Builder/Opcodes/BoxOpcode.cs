using System.Reflection.Emit;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class BoxOpcode : IOperationBuider
	{
		public int order => 999;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			container.RegisterOpBuilder(OpCodes.Box, new BoxOpcode());
		}
	}
}