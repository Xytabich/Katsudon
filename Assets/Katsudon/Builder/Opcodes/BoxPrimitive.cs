using System;
using System.Reflection.Emit;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class BoxPrimitive : IOperationBuider
	{
		public int order => 900;

		private PrimitiveConvertersList convertersList;

		public BoxPrimitive(PrimitiveConvertersList convertersList)
		{
			this.convertersList = convertersList;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var variable = method.PeekStack(0);
			if(NumberCodeUtils.IsConvertible(variable.type) && NumberCodeUtils.IsPrimitive(NumberCodeUtils.GetCode((Type)method.currentOp.argument)))
			{
				if(convertersList.TryConvert(method, variable, (Type)method.currentOp.argument, out var converted))
				{
					method.PopStack();
					method.PushStack(converted);
					return true;
				}
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			container.RegisterOpBuilder(OpCodes.Box, new BoxPrimitive(modules.GetModule<PrimitiveConvertersList>()));
		}
	}
}