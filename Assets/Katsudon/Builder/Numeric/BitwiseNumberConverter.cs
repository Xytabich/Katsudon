using System;
using Katsudon.Builder.Externs;

namespace Katsudon.Builder.Variables
{
	/// <summary>
	/// Builds a converter that converts one number to another bit by bit
	/// </summary>
	[NumberConverter]
	public class BitwiseNumberConverter : IFromNumberConverter
	{
		public int order => 100;

		public bool TryConvert(IMethodDescriptor method, in IVariable value, Type toType, out IVariable converted)
		{
			var toCode = Type.GetTypeCode(toType);
			if(!NumberCodeUtils.IsPrimitive(toCode) || !NumberCodeUtils.IsInteger(toCode))
			{
				converted = null;
				return false;
			}

			var fromCode = Type.GetTypeCode(value.type);
			var fromUnsigned = NumberCodeUtils.IsUnsigned(fromCode);
			var toUnsigned = NumberCodeUtils.IsUnsigned(toCode);

			var fromSize = NumberCodeUtils.GetSize(fromCode);
			var toSize = NumberCodeUtils.GetSize(toCode);

			var fromVariable = value;
			if(toSize > fromSize)
			{
				if(fromUnsigned || !toUnsigned)// unsigned>signed, signed>signed, unsigned>unsigned
				{
					converted = method.GetTmpVariable(toType);
					method.machine.ConvertExtern(fromVariable, converted);
					return true;
				}
				else
				{
					fromCode = NumberCodeUtils.ToSigned(toCode);
					fromVariable = method.GetTmpVariable(NumberCodeUtils.ToType(fromCode));
					method.machine.ConvertExtern(value, fromVariable);
					fromSize = NumberCodeUtils.GetSize(fromCode);
				}
			}
			else if(fromSize > toSize)
			{
				if(toUnsigned)
				{
					IVariable outVariable = null;
					var fromType = fromVariable.type;
					method.machine.BinaryOperatorExtern(
						BinaryOperator.LogicalAnd, fromVariable,
						method.machine.GetConstVariable(Convert.ChangeType(NumberCodeUtils.GetMaxValue(toCode), fromType)),
						fromType, () => (outVariable = method.GetTmpVariable(fromType))
					);
					converted = method.GetTmpVariable(toType);
					method.machine.ConvertExtern(outVariable, converted);
					return true;
				}
			}

			if(fromUnsigned) // unsigned -> signed
			{
				converted = BuildConverter(method, fromVariable, toType,
					method.machine.GetConstVariable(Convert.ChangeType(NumberCodeUtils.GetMaxValue(toCode), fromVariable.type)),
					method.machine.GetConstVariable(Convert.ChangeType(1u << (toSize * 8 - 1), fromVariable.type)),
					method.machine.GetConstVariable(NumberCodeUtils.GetMinValue(toCode))
				);
				return true;
			}
			else // signed -> unsigned
			{
				converted = BuildConverter(method, fromVariable, toType,
					method.machine.GetConstVariable(NumberCodeUtils.GetMaxValue(fromCode)),
					method.machine.GetConstVariable(NumberCodeUtils.GetMinValue(fromCode)),
					method.machine.GetConstVariable(Convert.ChangeType(1u << (fromSize * 8 - 1), toType))
				);
				return true;
			}
		}

		private static IVariable BuildConverter(IMethodDescriptor method, IVariable value, Type toType,
			IVariable unsignedBitsConst, IVariable fromSignBit, IVariable toSignBit)
		{
			// converted = Convert.To{toType}(value & unsignedBitsConst);
			// if((value & fromSignBit) != 0) converted |= toSignBit;

			IVariable tmp = method.GetTmpVariable(value.type);
			tmp.Allocate();
			value.Allocate();
			method.machine.BinaryOperatorExtern(BinaryOperator.LogicalAnd, value, unsignedBitsConst, tmp);
			var converted = method.GetTmpVariable(toType);
			converted.Allocate();
			method.machine.ConvertExtern(tmp, converted);

			method.machine.BinaryOperatorExtern(BinaryOperator.LogicalAnd, value, fromSignBit, tmp);
			var condition = method.GetTmpVariable(typeof(bool));
			method.machine.BinaryOperatorExtern(
				BinaryOperator.Inequality, tmp,
				method.machine.GetConstVariable(Convert.ChangeType(0, value.type)),
				condition
			);
			var exitLabel = new EmbedAddressLabel();
			method.machine.AddBranch(condition, exitLabel);
			method.machine.BinaryOperatorExtern(BinaryOperator.LogicalOr, converted, toSignBit, converted);
			method.machine.ApplyLabel(exitLabel);

			return converted;
		}

		public static void Register(NumericConvertersList container, IModulesContainer modules)
		{
			container.AddConverter(new BitwiseNumberConverter());
		}
	}
}