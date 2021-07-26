using System;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class ShrOpcode : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var b = method.PopStack();
			var a = method.PopStack();
			ProcessOp(method, method.currentOp.opCode == OpCodes.Shr_Un, a, b);
			return true;
		}

		public static void ProcessOp(IMethodDescriptor method, bool unsigned, IVariable a, IVariable b)
		{
			BinaryOperatorExtension.ShiftBinaryOperation(method, ProcessOperation, BinaryOperator.RightShift, unsigned, a, b);
		}

		private static object ProcessOperation(object a, int b, TypeCode code)
		{
			switch(code)
			{
				case TypeCode.Int64: return (long)a >> b;
				case TypeCode.UInt64: return (ulong)a >> b;
			}
			throw new InvalidOperationException(string.Format("Type code {0} is not supported", code));
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new ShrOpcode();
			container.RegisterOpBuilder(OpCodes.Shr, builder);
			container.RegisterOpBuilder(OpCodes.Shr_Un, builder);
		}
	}
}