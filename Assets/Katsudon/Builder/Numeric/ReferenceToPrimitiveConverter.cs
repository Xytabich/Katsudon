using System;
using Katsudon.Builder.Externs;

namespace Katsudon.Builder.Variables
{
	[PrimitiveConverter]
	public class ReferenceToPrimitiveConverter : IPrimitiveConverter
	{
		public int order => 82;

		public IVariable TryConvert(IUdonProgramBlock block, in IVariable variable, TypeCode fromPrimitive, TypeCode toPrimitive, Type toType)
		{
			if(fromPrimitive != TypeCode.Object) return null;
			if(!toType.IsPrimitive) return null;

			if(variable is IConstVariable constVariable)
			{
				return block.machine.GetConstVariable(Convert.ChangeType(constVariable.value == null ? 0 : 1, toPrimitive));
			}

			var tmp = block.GetTmpVariable(typeof(bool));
			block.machine.AddExtern("SystemObject.__ReferenceEquals__SystemObject_SystemObject__SystemBoolean",
				tmp, variable.OwnType(), block.machine.GetConstVariable(null).OwnType());
			tmp.Allocate();
			block.machine.UnaryOperatorExtern(UnaryOperator.UnaryNegation, tmp, tmp);
			var converted = block.GetTmpVariable(toType);
			if(toPrimitive == TypeCode.Char)
			{
				var tmpShort = block.GetTmpVariable(typeof(ushort));
				block.machine.ConvertExtern(tmp, tmpShort);
				tmp = tmpShort;
			}
			block.machine.ConvertExtern(tmp, converted);
			return converted;
		}

		public static void Register(PrimitiveConvertersList container, IModulesContainer modules)
		{
			container.AddConverter(new ReferenceToPrimitiveConverter());
		}
	}
}