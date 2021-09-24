using System;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class CleOpcode : IOperationBuider
	{
		public int order => 9;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			bool unsigned = method.currentOp.opCode == OpCodes.Cgt_Un;
			method.PushState();
			if(method.Next() && method.currentOp.opCode == OpCodes.Ldc_I4_0)
			{
				if(method.Next() && method.currentOp.opCode == OpCodes.Ceq)
				{
					method.DropState();

					var b = method.PopStack();
					var a = method.PopStack();

					ProcessOp(method, unsigned, a, b, () => method.GetOrPushOutVariable(typeof(bool)), out var constVariable);
					if(constVariable != null) method.PushStack(constVariable);
					return true;
				}
			}
			method.PopState();
			return false;
		}

		public static void ProcessOp(IMethodDescriptor method, bool? unsigned, IVariable a, IVariable b, Func<IVariable> retVariableCtor, out IVariable constVariable)
		{
			if(b is NullConstVariable)// strange "is null" check
			{
				var converted = retVariableCtor();
				method.machine.AddExtern("SystemObject.__ReferenceEquals__SystemObject_SystemObject__SystemBoolean",
					converted, a.OwnType(), method.machine.GetConstVariable(null).OwnType());
				constVariable = null;
				return;
			}
			BinaryOperatorExtension.LogicBinaryOperation(method, ProcessOperation, BinaryOperator.LessThanOrEqual, unsigned, a, b, retVariableCtor, out constVariable);
		}

		private static bool ProcessOperation(object a, object b, TypeCode code)
		{
			switch(code)
			{
				case TypeCode.Int64: return (long)a <= (long)b;
				case TypeCode.UInt64: return (ulong)a <= (ulong)b;
				case TypeCode.Double: return (double)a <= (double)b;
			}
			throw new InvalidOperationException(string.Format("Type code {0} is not supported", code));
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new CleOpcode();
			container.RegisterOpBuilder(OpCodes.Cgt, builder);
			container.RegisterOpBuilder(OpCodes.Cgt_Un, builder);
		}
	}
}