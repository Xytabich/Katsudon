using System;
using System.Reflection.Emit;
using Katsudon.Builder.Converters;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class NewarrOpcode : IOperationBuider
	{
		public int order => 100;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var len = method.PopStack();

			var elementType = (Type)method.currentOp.argument;
			var arrType = elementType.MakeArrayType();
			if(UdonValueResolver.instance.TryGetUdonType(arrType, out var udonType))
			{
				method.machine.AddExtern(
					Utils.GetExternName(udonType, "__ctor__SystemInt32__{0}", udonType),
					method.GetOrPushOutVariable(arrType),
					len.UseType(typeof(int))
				);
			}
			else
			{
				throw new System.Exception(string.Format("Array type {0} is not supported by udon", arrType));
			}
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new NewarrOpcode();
			container.RegisterOpBuilder(OpCodes.Newarr, builder);
		}
	}
}