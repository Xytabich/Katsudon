using System.Reflection.Emit;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class StelemNOpcode : IOperationBuider
	{
		public int order => 0;

		private System.Type type;

		public StelemNOpcode(System.Type type)
		{
			this.type = type;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var value = method.PopStack();
			var index = method.PopStack();
			var array = method.PopStack();

			var elementType = array.type.GetElementType();
			method.machine.AddExtern(
				Utils.GetExternName(array.type, "__Set__SystemInt32_{0}__SystemVoid", elementType),
				array.OwnType(),
				index.UseType(typeof(int)),
				value.UseType(elementType)
			);

			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			container.RegisterOpBuilder(OpCodes.Stelem_I1, new StelemNOpcode(typeof(sbyte)));
			container.RegisterOpBuilder(OpCodes.Stelem_I2, new StelemNOpcode(typeof(short)));
			container.RegisterOpBuilder(OpCodes.Stelem_I4, new StelemNOpcode(typeof(int)));
			container.RegisterOpBuilder(OpCodes.Stelem_I8, new StelemNOpcode(typeof(long)));

			container.RegisterOpBuilder(OpCodes.Stelem_R4, new StelemNOpcode(typeof(float)));
			container.RegisterOpBuilder(OpCodes.Stelem_R8, new StelemNOpcode(typeof(double)));
		}
	}
}