using System;
using Katsudon.Builder.Externs;

namespace Katsudon.Builder.Variables
{
	[NumberConverter]
	public class BoolVariableConverter : IPrimitiveConverter
	{
		public int order => 81;

		public bool TryConvert(IUdonProgramBlock block, in IVariable variable, Type toType, out IVariable converted)
		{
			if(toType != typeof(bool))
			{
				converted = null;
				return false;
			}
			converted = block.GetTmpVariable(typeof(bool));
			if(variable.type.IsPrimitive)
			{
				block.machine.ConvertExtern(variable, converted);
			}
			else if(variable.type.IsValueType)
			{
				block.machine.AddExtern(ConvertExtension.GetExternName(typeof(object), typeof(bool)), converted, variable.OwnType());
			}
			else//TODO: definitely need a rework of primitive converters system, since clr processes raw data, which is actually links, numbers, etc.
			{
				block.machine.AddExtern("SystemObject.__ReferenceEquals__SystemObject_SystemObject__SystemBoolean",
					converted, variable.OwnType(), block.machine.GetConstVariable(null).OwnType());
				converted.Allocate();
				block.machine.UnaryOperatorExtern(UnaryOperator.UnaryNegation, converted, converted);
			}
			return true;
		}

		public static void Register(PrimitiveConvertersList container, IModulesContainer modules)
		{
			container.AddConverter(new BoolVariableConverter());
		}
	}
}