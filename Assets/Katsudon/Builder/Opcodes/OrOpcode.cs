using System;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class OrOpcode : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var b = method.PopStack();
			var a = method.PopStack();
			ProcessOp(method, a, b);
			return true;
		}

		public static void ProcessOp(IMethodDescriptor method, IVariable a, IVariable b)
		{
			BinaryOperatorExtension.ArithmeticBinaryOperation(method, ProcessOperation, BinaryOperator.LogicalOr, null, a, b);
		}

		private static object ProcessOperation(object a, object b, TypeCode code)
		{
			switch(code)
			{
				case TypeCode.Int64: return (long)a | (long)b;
				case TypeCode.UInt64: return (ulong)a | (ulong)b;
			}
			throw new InvalidOperationException(string.Format("Type code {0} is not supported", code));
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new OrOpcode();
			container.RegisterOpBuilder(OpCodes.Or, builder);
		}
	}
}