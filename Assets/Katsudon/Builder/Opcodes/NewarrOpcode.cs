using System;
using System.Reflection.Emit;
using Katsudon.Builder.Variables;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class NewarrOpcode : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var len = method.PopStack();

			var elementType = (Type)method.currentOp.argument;
			var arrType = elementType.MakeArrayType();
			var udonType = ArrayTypes.GetUdonArrayType(arrType);
			method.machine.AddExtern(
				Utils.GetExternName(udonType, "__ctor__SystemInt32__{0}", udonType),
				method.GetOrPushOutVariable(arrType),
				len.UseType(typeof(int))
			);
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new NewarrOpcode();
			container.RegisterOpBuilder(OpCodes.Newarr, builder);
		}
	}
}