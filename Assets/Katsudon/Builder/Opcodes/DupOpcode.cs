using System;
using System.Reflection.Emit;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class DupOpcode : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var variable = method.PeekStack(0);
			variable.Allocate();
			method.PushStack(variable);
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			container.RegisterOpBuilder(OpCodes.Dup, new DupOpcode());
		}
	}
}