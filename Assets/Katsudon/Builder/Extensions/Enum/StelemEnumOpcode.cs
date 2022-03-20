using System;
using System.Reflection.Emit;

namespace Katsudon.Builder.Extensions.EnumExtension
{
	[OperationBuilder]
	public class StelemEnumOpcode : IOperationBuider
	{
		public int order => 50;

		private System.Type type;

		public StelemEnumOpcode(System.Type type)
		{
			this.type = type;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var arrayType = method.PeekStack(2).type;
			if(Utils.IsUdonType(arrayType)) return false;
			var elementType = arrayType.GetElementType();
			if(!elementType.IsEnum) return false;

			var value = method.PopStack();
			var index = method.PopStack();
			var array = method.PopStack();

			elementType = Enum.GetUnderlyingType(elementType);
			arrayType = elementType.MakeArrayType();
			method.machine.AddExtern(
				Utils.GetExternName(arrayType, "__Set__SystemInt32_{0}__SystemVoid", elementType),
				array.OwnType(),
				index.UseType(typeof(int)),
				value.UseType(elementType)
			);

			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			container.RegisterOpBuilder(OpCodes.Stelem_I1, new StelemEnumOpcode(typeof(sbyte)));
			container.RegisterOpBuilder(OpCodes.Stelem_I2, new StelemEnumOpcode(typeof(short)));
			container.RegisterOpBuilder(OpCodes.Stelem_I4, new StelemEnumOpcode(typeof(int)));
			container.RegisterOpBuilder(OpCodes.Stelem_I8, new StelemEnumOpcode(typeof(long)));

			container.RegisterOpBuilder(OpCodes.Stelem_R4, new StelemEnumOpcode(typeof(float)));
			container.RegisterOpBuilder(OpCodes.Stelem_R8, new StelemEnumOpcode(typeof(double)));
		}
	}
}