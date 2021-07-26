using System;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class NegOpcode : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			UnaryOperatorExtension.NumberUnaryOperation(method, ProcessOperation, UnaryOperator.UnaryMinus, method.PopStack());
			return true;
		}

		private static object ProcessOperation(object a, TypeCode code)
		{
			switch(code)
			{
				case TypeCode.Int64: return -(long)a;
				case TypeCode.Double: return -(double)a;
			}
			throw new InvalidOperationException(string.Format("Type code {0} is not supported", code));
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new NegOpcode();
			container.RegisterOpBuilder(OpCodes.Neg, builder);
		}
	}
}