using System;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class NotOpcode : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			UnaryOperatorExtension.NumberUnaryOperation(method, ProcessOperation, UnaryOperator.UnaryNegation, method.PopStack());
			return true;
		}

		private static object ProcessOperation(object a, TypeCode code)
		{
			switch(code)
			{
				case TypeCode.Int64: return ~(long)a;
				case TypeCode.UInt64: return ~(ulong)a;
			}
			throw new InvalidOperationException(string.Format("Type code {0} is not supported", code));
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new NotOpcode();
			container.RegisterOpBuilder(OpCodes.Not, builder);
		}
	}
}