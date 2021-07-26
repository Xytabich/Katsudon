using System;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class CltOpcode : IOperationBuider
	{
		public int order => 10;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var b = method.PopStack();
			var a = method.PopStack();
			ProcessOp(method, method.currentOp.opCode == OpCodes.Clt_Un, a, b, () => method.GetOrPushOutVariable(typeof(bool)), out var constVariable);
			if(constVariable != null) method.PushStack(constVariable);
			return true;
		}

		public static void ProcessOp(IMethodDescriptor method, bool? unsigned, IVariable a, IVariable b, Func<IVariable> retVariableCtor, out IVariable constVariable)
		{
			BinaryOperatorExtension.LogicBinaryOperation(method, ProcessOperation, BinaryOperator.LessThan, unsigned, a, b, retVariableCtor, out constVariable);
		}

		private static bool ProcessOperation(object a, object b, TypeCode code)
		{
			switch(code)
			{
				case TypeCode.Int64: return (long)a < (long)b;
				case TypeCode.UInt64: return (ulong)a < (ulong)b;
				case TypeCode.Double: return (double)a < (double)b;
			}
			throw new InvalidOperationException(string.Format("Type code {0} is not supported", code));
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new CltOpcode();
			container.RegisterOpBuilder(OpCodes.Clt, builder);
			container.RegisterOpBuilder(OpCodes.Clt_Un, builder);
		}
	}
}