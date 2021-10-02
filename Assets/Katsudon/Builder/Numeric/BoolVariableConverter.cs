using System;
using Katsudon.Builder.Externs;

namespace Katsudon.Builder.Variables
{
	[PrimitiveConverter]
	public class BoolVariableConverter : IPrimitiveConverter
	{
		public int order => 81;

		public IVariable TryConvert(IUdonProgramBlock block, in IVariable variable, TypeCode fromPrimitive, TypeCode toPrimitive, Type toType)
		{
			if(toType != typeof(bool)) return null;

			var converted = block.GetTmpVariable(typeof(bool));
			if(fromPrimitive == TypeCode.Char)
			{
				var tmp = block.GetTmpVariable(typeof(ushort));
				if(variable.type.IsPrimitive)
				{
					block.machine.ConvertExtern(variable, tmp);
				}
				else
				{
					block.machine.AddExtern(ConvertExtension.GetExternName(typeof(object), typeof(bool)), tmp, variable.OwnType());
				}
				block.machine.ConvertExtern(tmp, converted);
			}
			else if(fromPrimitive == TypeCode.Object)
			{
				block.machine.AddExtern("SystemObject.__ReferenceEquals__SystemObject_SystemObject__SystemBoolean",
					converted, variable.OwnType(), block.machine.GetConstVariable(null).OwnType());
				converted.Allocate();
				block.machine.UnaryOperatorExtern(UnaryOperator.UnaryNegation, converted, converted);
			}
			else if(variable.type.IsPrimitive)
			{
				block.machine.ConvertExtern(variable, converted);
			}
			else
			{
				block.machine.AddExtern(ConvertExtension.GetExternName(typeof(object), typeof(bool)), converted, variable.OwnType());
			}
			return converted;
		}

		public static void Register(PrimitiveConvertersList container, IModulesContainer modules)
		{
			container.AddConverter(new BoolVariableConverter());
		}
	}
}