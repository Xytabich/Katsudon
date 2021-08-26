using System;

namespace Katsudon.Builder.Externs
{
	public static class UnaryOperatorExtension
	{
		public static void UnaryOperatorExtern(this IUdonMachine machine, UnaryOperator type,
			IVariable variable, IVariable outVariable)
		{
			machine.AddExtern(
				GetExternName(type, variable.type, outVariable.type),
				outVariable,
				variable.OwnType()
			);
		}

		public static void UnaryOperatorExtern(this IUdonMachine machine, UnaryOperator type,
			VariableMeta variable, Type outType, Func<IVariable> outVariableCtor)
		{
			machine.AddExtern(
				GetExternName(type, variable.preferredType, outType),
				outVariableCtor,
				variable
			);
		}

		public static void NumberUnaryOperation(IMethodDescriptor method, Func<object, TypeCode, object> constCtor, UnaryOperator operationType, IVariable value)
		{
			var cvalue = value as IConstVariable;
			if(cvalue != null)
			{
				var aCode = NumberCodeUtils.GetCode(cvalue.type);
				bool real = NumberCodeUtils.IsFloat(aCode);

				method.PushStack(method.machine.GetConstVariable(constCtor(
					GetTypedNumberValue(cvalue.value, aCode, real),
					real ? TypeCode.Double : (NumberCodeUtils.IsUnsigned(aCode) ? TypeCode.UInt64 : TypeCode.Int64)
				)));
			}
			else
			{
				method.machine.UnaryOperatorExtern(operationType, value.OwnType(), value.type, () => method.GetOrPushOutVariable(value.type));
			}
		}

		public static string GetExternName(UnaryOperator operation, Type inType, Type outType)
		{
			return Utils.GetExternName(inType, "__op_" + operation + "__{0}__{1}", inType, outType);
		}

		private static object GetTypedNumberValue(object value, TypeCode valueCode, bool real)
		{
			if(real) return Convert.ToDouble(value);
			if(NumberCodeUtils.IsUnsigned(valueCode))
			{
				return Convert.ToUInt64(value);
			}
			else
			{
				return Convert.ToInt64(value);
			}
		}
	}

	public enum UnaryOperator
	{
		UnaryMinus,
		UnaryNegation
	}
}