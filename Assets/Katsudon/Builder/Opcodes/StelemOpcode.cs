using System.Reflection.Emit;
using Katsudon.Builder.Variables;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class StelemOpcode : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var value = method.PopStack();
			var index = method.PopStack();
			var array = method.PopStack();

			var elementType = array.type.GetElementType();
			var arrType = ArrayTypes.GetUdonArrayType(array.type);
			method.machine.AddExtern(
				Utils.GetExternName(arrType, "__Set__SystemInt32_{0}__SystemVoid", arrType.GetElementType()),
				array.OwnType(),
				index.UseType(typeof(int)),
				value.UseType(elementType)
			);

			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new StelemOpcode();
			container.RegisterOpBuilder(OpCodes.Stelem, builder);
			container.RegisterOpBuilder(OpCodes.Stelem_Ref, builder);
		}
	}
}