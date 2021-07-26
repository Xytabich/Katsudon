using System.Reflection.Emit;
using Katsudon.Builder.Variables;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class LdelemOpcode : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var index = method.PopStack();
			var array = method.PopStack();

			var elementType = array.type.GetElementType();
			var arrType = ArrayTypes.GetUdonArrayType(array.type);
			method.machine.AddExtern(
				Utils.GetExternName(arrType, "__Get__SystemInt32__{0}", arrType.GetElementType()),
				() => method.GetOrPushOutVariable(elementType),
				array.OwnType(),
				index.UseType(typeof(int))
			);

			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new LdelemOpcode();
			container.RegisterOpBuilder(OpCodes.Ldelem_I1, builder);
			container.RegisterOpBuilder(OpCodes.Ldelem_I2, builder);
			container.RegisterOpBuilder(OpCodes.Ldelem_I4, builder);
			container.RegisterOpBuilder(OpCodes.Ldelem_I8, builder);

			container.RegisterOpBuilder(OpCodes.Ldelem_U1, builder);
			container.RegisterOpBuilder(OpCodes.Ldelem_U2, builder);
			container.RegisterOpBuilder(OpCodes.Ldelem_U4, builder);

			container.RegisterOpBuilder(OpCodes.Ldelem_R4, builder);
			container.RegisterOpBuilder(OpCodes.Ldelem_R8, builder);

			container.RegisterOpBuilder(OpCodes.Ldelem, builder);
			container.RegisterOpBuilder(OpCodes.Ldelem_Ref, builder);
		}
	}
}