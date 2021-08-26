using System;
using System.Reflection.Emit;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class BoxPrimitive : IOperationBuider
	{
		public int order => 900;

		private NumericConvertersList convertersList;

		public BoxPrimitive(NumericConvertersList convertersList)
		{
			this.convertersList = convertersList;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var variable = method.PeekStack(0);
			if(NumberCodeUtils.IsPrimitive(NumberCodeUtils.GetCode(variable.type)) ||
				NumberCodeUtils.IsPrimitive(NumberCodeUtils.GetCode((Type)method.currentOp.argument)))
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
			container.RegisterOpBuilder(OpCodes.Box, new BoxPrimitive(modules.GetModule<NumericConvertersList>()));
		}
	}
}