using System.Reflection.Emit;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class StindOpcode : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var variable = method.PopStack();
			var target = method.PopStack();
			method.machine.AddCopy(variable, target);
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new StindOpcode();
			container.RegisterOpBuilder(OpCodes.Stind_I1, builder);
			container.RegisterOpBuilder(OpCodes.Stind_I2, builder);
			container.RegisterOpBuilder(OpCodes.Stind_I4, builder);
			container.RegisterOpBuilder(OpCodes.Stind_I, builder);
			container.RegisterOpBuilder(OpCodes.Stind_I8, builder);
			container.RegisterOpBuilder(OpCodes.Stind_R4, builder);
			container.RegisterOpBuilder(OpCodes.Stind_R8, builder);
			container.RegisterOpBuilder(OpCodes.Stind_Ref, builder);
		}
	}
}