using System;

namespace Katsudon.Builder.Externs
{
	public static class BinaryOperatorExtension
	{
		public static void BinaryOperatorExtern(this IUdonMachine machine, BinaryOperator type,
			IVariable leftVariable, IVariable rightVariable, IVariable outVariable)
		{
#if KATSUDON_DEBUG
			if(outVariable.type != typeof(bool) && NumberCodeUtils.GetSize(NumberCodeUtils.GetCode(outVariable.type)) < sizeof(int))
			{
				throw new Exception("Invalid output type: " + outVariable.type);
			}
#endif
			machine.AddExtern(
				GetExternName(type, leftVariable.type, rightVariable.type, outVariable.type),
				outVariable,
				leftVariable.OwnType(),
				rightVariable.OwnType()
			);
		}

		public static void BinaryOperatorExtern(this IUdonMachine machine, BinaryOperator type,
			IVariable leftVariable, IVariable rightVariable, Type outType, Func<IVariable> outVariableCtor)
		{
#if KATSUDON_DEBUG
			if(outType != typeof(bool) && NumberCodeUtils.GetSize(NumberCodeUtils.GetCode(outType)) < sizeof(int))
			{
				throw new Exception("Invalid output type: " + outType);
			}
#endif
			machine.AddExtern(
				GetExternName(type, leftVariable.type, rightVariable.type, outType),
				outVariableCtor,
				leftVariable.OwnType(),
				rightVariable.OwnType()
			);
		}

		public static void BinaryOperatorExtern(this IUdonMachine machine, BinaryOperator type,
			VariableMeta leftVariable, VariableMeta rightVariable, Type outType, Func<IVariable> outVariableCtor)
		{
#if KATSUDON_DEBUG
			if(outType != typeof(bool) && NumberCodeUtils.GetSize(NumberCodeUtils.GetCode(outType)) < sizeof(int))
			{
				throw new Exception("Invalid output type: " + outType);
			}
#endif
			machine.AddExtern(
				GetExternName(type, leftVariable.preferredType, rightVariable.preferredType, outType),
				outVariableCtor,
				leftVariable,
				rightVariable
			);
		}

		public static void ArithmeticBinaryOperation(IMethodDescriptor method, Func<object, object, TypeCode, object> constCtor,
			BinaryOperator operationType, bool? unsigned, IVariable a, IVariable b)
		{
			var ac = a as IConstVariable;
			var bc = b as IConstVariable;
			if(ac != null && bc != null)
			{
				var aCode = NumberCodeUtils.GetCode(ac.type);
				var bCode = NumberCodeUtils.GetCode(bc.type);
				bool real = NumberCodeUtils.IsFloat(aCode) || NumberCodeUtils.IsFloat(bCode);
				bool unsig = unsigned.HasValue ? unsigned.Value : (NumberCodeUtils.IsUnsigned(aCode) || NumberCodeUtils.IsUnsigned(bCode));
				method.PushStack(method.machine.GetConstVariable(constCtor(
					GetTypedNumberValue(ac.value, aCode, real, unsig),
					GetTypedNumberValue(bc.value, bCode, real, unsig),
					real ? TypeCode.Double : (unsig ? TypeCode.UInt64 : TypeCode.Int64)
				)));
			}
			else
			{
				var code = Type.GetTypeCode(a.type);
				if(a.type != b.type && bc == null && (ac != null || NumberCodeUtils.GetSize(Type.GetTypeCode(b.type)) > NumberCodeUtils.GetSize(code)))
				{
					code = Type.GetTypeCode(b.type);
				}
				if(unsigned.HasValue)
				{
					if(NumberCodeUtils.IsUnsigned(code) != unsigned.Value)
					{
						code = unsigned.Value ? NumberCodeUtils.ToUnsigned(code) : NumberCodeUtils.ToSigned(code);
					}
				}
				Type type, outputType = type = NumberCodeUtils.ToType(code);
				if(NumberCodeUtils.GetSize(code) < sizeof(int)) outputType = typeof(int);
				method.machine.BinaryOperatorExtern(operationType, a.UseType(type), b.UseType(type), outputType, () => method.GetOrPushOutVariable(outputType));
			}
		}

		public static void ArithmeticBinaryOperation(IUdonMachine machine, BinaryOperator operationType, IVariable a, IVariable b, Func<Type, IVariable> outVariableCtor)
		{
			var code = Type.GetTypeCode(a.type);
			if(code != Type.GetTypeCode(b.type)) throw new Exception("The input data types do not match");

			Type type, outputType = type = NumberCodeUtils.ToType(code);
			if(NumberCodeUtils.GetSize(code) < sizeof(int)) outputType = typeof(int);
			machine.BinaryOperatorExtern(operationType, a.UseType(type), b.UseType(type), outputType, () => outVariableCtor(outputType));
		}

		public static void ShiftBinaryOperation(IMethodDescriptor method, Func<object, int, TypeCode, object> constCtor,
			BinaryOperator operationType, bool? unsigned, IVariable a, IVariable b)
		{
			var ac = a as IConstVariable;
			var bc = b as IConstVariable;
			if(ac != null && bc != null)
			{
				var aCode = NumberCodeUtils.GetCode(ac.type);
				bool unsig = unsigned.HasValue ? unsigned.Value : NumberCodeUtils.IsUnsigned(aCode);
				method.PushStack(method.machine.GetConstVariable(constCtor(
					GetTypedNumberValue(ac.value, aCode, false, unsig),
					Convert.ToInt32(bc.value),
					unsig ? TypeCode.UInt64 : TypeCode.Int64
				)));
			}
			else
			{
				ShiftBinaryOperation(method.machine, operationType, a, b, (type) => method.GetOrPushOutVariable(type));
			}
		}

		public static void ShiftBinaryOperation(IUdonMachine machine, BinaryOperator operationType, IVariable a, IVariable b, Func<Type, IVariable> outVariableCtor)
		{
			var code = Type.GetTypeCode(a.type);
			Type type = NumberCodeUtils.ToType(code);
			Type outputType = type;
			Type offsetType = type;
			if(NumberCodeUtils.GetSize(code) < sizeof(int)) outputType = typeof(int);
			if(NumberCodeUtils.GetSize(code) >= sizeof(int)) offsetType = typeof(int);// why..?
			machine.BinaryOperatorExtern(operationType, a.UseType(type), b.UseType(offsetType), outputType, () => outVariableCtor(outputType));
		}

		public static void LogicBinaryOperation(IMethodDescriptor method, Func<object, object, TypeCode, bool> constCtor,
			BinaryOperator operationType, bool? unsigned, IVariable a, IVariable b, Func<IVariable> retVariableCtor, out IVariable constVariable)
		{
			var ac = a as IConstVariable;
			var bc = b as IConstVariable;
			if(ac != null && bc != null)
			{
				var aCode = NumberCodeUtils.GetCode(ac.type);
				var bCode = NumberCodeUtils.GetCode(bc.type);
				bool real = NumberCodeUtils.IsFloat(aCode) || NumberCodeUtils.IsFloat(bCode);
				bool unsig = unsigned.HasValue ? unsigned.Value : (NumberCodeUtils.IsUnsigned(aCode) || NumberCodeUtils.IsUnsigned(bCode));

				constVariable = method.machine.GetConstVariable(constCtor(
					GetTypedNumberValue(ac.value, aCode, real, unsig),
					GetTypedNumberValue(bc.value, bCode, real, unsig),
					real ? TypeCode.Double : (unsig ? TypeCode.UInt64 : TypeCode.Int64)
				));
			}
			else
			{
				Type type = a.type;
				if(a.type != b.type && bc == null && (ac != null ||
					NumberCodeUtils.GetSize(NumberCodeUtils.GetCode(b.type)) > NumberCodeUtils.GetSize(NumberCodeUtils.GetCode(a.type))))
				{
					type = b.type;
				}
				if(unsigned.HasValue)
				{
					var code = NumberCodeUtils.GetCode(type);
					if(NumberCodeUtils.IsUnsigned(code) != unsigned.Value)
					{
						type = NumberCodeUtils.ToType(unsigned.Value ? NumberCodeUtils.ToUnsigned(code) : NumberCodeUtils.ToSigned(code));
					}
				}

				constVariable = null;
				type = NumberCodeUtils.ToPrimitive(type);
				method.machine.BinaryOperatorExtern(operationType, a.UseType(type), b.UseType(type), typeof(bool), retVariableCtor);
			}
		}

		public static string GetExternName(BinaryOperator type, Type leftType, Type rightType, Type outType)
		{
			return Utils.GetExternName(leftType, "__op_" + type + "__{0}_{1}__{2}", leftType, rightType, outType);
		}

		public static Type GetOutputType(Type reference)
		{
			var code = Type.GetTypeCode(reference);
			return NumberCodeUtils.ToType(NumberCodeUtils.GetSize(code) < sizeof(int) ? TypeCode.Int32 : code);
		}

		private static object GetTypedNumberValue(object value, TypeCode valueCode, bool real, bool unsigned)
		{
			if(real) return Convert.ToDouble(value);
			if(NumberCodeUtils.IsUnsigned(valueCode))
			{
				var n = Convert.ToUInt64(value);
				if(unsigned) return n;
				else return (long)n;
			}
			else
			{
				var n = Convert.ToInt64(value);
				if(unsigned) return (ulong)n;
				else return n;
			}
		}
	}

	public enum BinaryOperator
	{
		Addition,
		Subtraction,
		Multiplication,
		Division,
		Equality,
		Inequality,
		LessThan,
		GreaterThan,
		LessThanOrEqual,
		GreaterThanOrEqual,
		LeftShift,
		RightShift,
		LogicalAnd,
		LogicalOr,
		LogicalXor,
		Remainder
	}
}