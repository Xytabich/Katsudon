using System.Reflection.Emit;
using Katsudon.Builder.Converters;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class StelemOpcode : IOperationBuider
	{
		public int order => 100;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var value = method.PopStack();
			var index = method.PopStack();
			var array = method.PopStack();

			var elementType = array.type.GetElementType();
			if(UdonValueResolver.instance.TryGetUdonType(array.type, out var arrType))
			{
				method.machine.AddExtern(
					Utils.GetExternName(arrType, "__Set__SystemInt32_{0}__SystemVoid", arrType.GetElementType()),
					array.OwnType(),
					index.UseType(typeof(int)),
					value.UseType(elementType)
				);
			}
			else
			{
				throw new System.Exception(string.Format("Array type {0} is not supported by udon", array.type));
			}

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