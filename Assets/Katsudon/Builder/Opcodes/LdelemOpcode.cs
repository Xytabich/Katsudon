using System.Reflection.Emit;
using Katsudon.Builder.Converters;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class LdelemOpcode : IOperationBuider
	{
		public int order => 100;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var index = method.PopStack();
			var array = method.PopStack();

			var elementType = array.type.GetElementType();
			if(UdonValueResolver.instance.TryGetUdonType(array.type, out var arrType))
			{
				method.machine.AddExtern(
					Utils.GetExternName(arrType, "__Get__SystemInt32__{0}", arrType.GetElementType()),
					() => method.GetOrPushOutVariable(elementType),
					array.OwnType(),
					index.UseType(typeof(int))
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