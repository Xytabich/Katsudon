using System;
using Katsudon.Builder.Externs;

namespace Katsudon.Builder.Variables
{
	/// <summary>
	/// Builds a converter that converts one number to another bit by bit
	/// </summary>
	[PrimitiveConverter]
	public class BitwiseNumberConverter : IPrimitiveConverter
	{
		public int order => 100;

		public IVariable TryConvert(IUdonProgramBlock block, in IVariable variable, TypeCode fromPrimitive, TypeCode toPrimitive, Type toType)
		{
			if(fromPrimitive == TypeCode.Object) return null;
			if(!toType.IsPrimitive) return null;
			if(!NumberCodeUtils.IsInteger(toPrimitive)) return null;

			var value = variable;
			if(!value.type.IsPrimitive)
			{
				var primitiveType = NumberCodeUtils.ToType(fromPrimitive);
				value = block.GetTmpVariable(primitiveType);
				block.machine.AddExtern(ConvertExtension.GetExternName(typeof(object), primitiveType), value, variable.OwnType());
			}

			var fromUnsigned = NumberCodeUtils.IsUnsigned(fromPrimitive);
			var toUnsigned = NumberCodeUtils.IsUnsigned(toPrimitive);

			var fromSize = NumberCodeUtils.GetSize(fromPrimitive);
			var toSize = NumberCodeUtils.GetSize(toPrimitive);

			var fromVariable = value;
			if(toSize > fromSize)
			{
				if(fromUnsigned || !toUnsigned)// unsigned>signed, signed>signed, unsigned>unsigned
				{
					var converted = block.GetTmpVariable(toType);
					block.machine.ConvertExtern(fromVariable, converted);
					return converted;
				}
				else
				{
					fromPrimitive = NumberCodeUtils.ToSigned(toPrimitive);
					fromVariable = block.GetTmpVariable(NumberCodeUtils.ToType(fromPrimitive));
					block.machine.ConvertExtern(value, fromVariable);
					fromSize = NumberCodeUtils.GetSize(fromPrimitive);
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
						block.machine.GetConstVariable(Convert.ChangeType(NumberCodeUtils.GetMaxValue(toPrimitive), fromType)),
						fromType, () => (outVariable = block.GetTmpVariable(fromType))
					);
					var converted = block.GetTmpVariable(toType);
					block.machine.ConvertExtern(outVariable, converted);
					return converted;
				}
			}

			if(fromUnsigned) // unsigned -> signed
			{
				return BuildConverter(block, fromVariable, toType,
					block.machine.GetConstVariable(Convert.ChangeType(NumberCodeUtils.GetMaxValue(toPrimitive), fromVariable.type)),
					block.machine.GetConstVariable(Convert.ChangeType(1u << (toSize * 8 - 1), fromVariable.type)),
					block.machine.GetConstVariable(NumberCodeUtils.GetMinValue(toPrimitive))
				);
			}
			else // signed -> unsigned
			{
				return BuildConverter(block, fromVariable, toType,
					block.machine.GetConstVariable(NumberCodeUtils.GetMaxValue(fromPrimitive)),
					block.machine.GetConstVariable(NumberCodeUtils.GetMinValue(fromPrimitive)),
					block.machine.GetConstVariable(Convert.ChangeType(1u << (fromSize * 8 - 1), toType))
				);
			}
		}

		private static IVariable BuildConverter(IUdonProgramBlock block, IVariable value, Type toType,
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

		public static void Register(PrimitiveConvertersList container, IModulesContainer modules)
		{
			container.AddConverter(new BitwiseNumberConverter());
		}
	}
}