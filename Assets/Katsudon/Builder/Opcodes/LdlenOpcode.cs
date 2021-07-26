using System;
using System.Reflection.Emit;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class LdlenOpcode : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var array = method.PopStack();

			method.machine.AddExtern(
				Utils.GetExternName(typeof(Array), "__get_Length__SystemInt32"),
				method.GetOrPushOutVariable(typeof(int)),
				array.OwnType()
			);

			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new LdlenOpcode();
			container.RegisterOpBuilder(OpCodes.Ldlen, builder);
		}
	}
}