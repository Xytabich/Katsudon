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

		public bool TryConvert(IMachineBlock block, in IVariable value, Type toType, out IVariable converted)
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
					converted = block.GetTmpVariable(toType);
					block.machine.ConvertExtern(fromVariable, converted);
					return true;
				}
				else
				{
					fromCode = NumberCodeUtils.ToSigned(toCode);
					fromVariable = block.GetTmpVariable(NumberCodeUtils.ToType(fromCode));
					block.machine.ConvertExtern(value, fromVariable);
					fromSize = NumberCodeUtils.GetSize(fromCode);
				}
			}
			else if(fromSize > toSize)
			{
				if(toUnsigned)
				{
					IVariable outVariable = null;
					var fromType = fromVariable.type;
					block.machine.BinaryOperatorExtern(
						BinaryOperator.LogicalAnd, fromVariable,
						block.machine.GetConstVariable(Convert.ChangeType(NumberCodeUtils.GetMaxValue(toCode), fromType)),
						fromType, () => (outVariable = block.GetTmpVariable(fromType))
					);
					converted = block.GetTmpVariable(toType);
					block.machine.ConvertExtern(outVariable, converted);
					return true;
				}
			}

			if(fromUnsigned) // unsigned -> signed
			{
				converted = BuildConverter(block, fromVariable, toType,
					block.machine.GetConstVariable(Convert.ChangeType(NumberCodeUtils.GetMaxValue(toCode), fromVariable.type)),
					block.machine.GetConstVariable(Convert.ChangeType(1u << (toSize * 8 - 1), fromVariable.type)),
					block.machine.GetConstVariable(NumberCodeUtils.GetMinValue(toCode))
				);
				return true;
			}
			else // signed -> unsigned
			{
				converted = BuildConverter(block, fromVariable, toType,
					block.machine.GetConstVariable(NumberCodeUtils.GetMaxValue(fromCode)),
					block.machine.GetConstVariable(NumberCodeUtils.GetMinValue(fromCode)),
					block.machine.GetConstVariable(Convert.ChangeType(1u << (fromSize * 8 - 1), toType))
				);
				return true;
			}
		}

		private static IVariable BuildConverter(IMachineBlock block, IVariable value, Type toType,
			IVariable unsignedBitsConst, IVariable fromSignBit, IVariable toSignBit)
		{
			// converted = Convert.To{toType}(value & unsignedBitsConst);
			// if((value & fromSignBit) != 0) converted |= toSignBit;

			IVariable tmp = block.GetTmpVariable(value.type);
			tmp.Allocate();
			value.Allocate();
			block.machine.BinaryOperatorExtern(BinaryOperator.LogicalAnd, value, unsignedBitsConst, tmp);
			var converted = block.GetTmpVariable(toType);
			converted.Allocate();
			block.machine.ConvertExtern(tmp, converted);

			block.machine.BinaryOperatorExtern(BinaryOperator.LogicalAnd, value, fromSignBit, tmp);
			var condition = block.GetTmpVariable(typeof(bool));
			block.machine.BinaryOperatorExtern(
				BinaryOperator.Inequality, tmp,
				block.machine.GetConstVariable(Convert.ChangeType(0, value.type)),
				condition
			);
			var exitLabel = new EmbedAddressLabel();
			block.machine.AddBranch(condition, exitLabel);
			block.machine.BinaryOperatorExtern(BinaryOperator.LogicalOr, converted, toSignBit, converted);
			block.machine.ApplyLabel(exitLabel);

			return converted;
		}

		public static void Register(NumericConvertersList container, IModulesContainer modules)
		{
			container.AddConverter(new BitwiseNumberConverter());
		}
	}
}