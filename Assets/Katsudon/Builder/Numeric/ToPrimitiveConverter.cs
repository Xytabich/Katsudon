using System;
using Katsudon.Builder.Externs;

namespace Katsudon.Builder.Variables
{
	[PrimitiveConverter]
	public class ToPrimitiveConverter : IPrimitiveConverter
	{
		public int order => 110;

		public IVariable TryConvert(IUdonProgramBlock block, in IVariable variable, TypeCode fromPrimitive, TypeCode toPrimitive, Type toType)
		{
			if(fromPrimitive == TypeCode.Object || !toType.IsPrimitive || fromPrimitive != toPrimitive)
			{
				return null;
			}

			var converted = block.GetTmpVariable(toType);
			block.machine.AddExtern(ConvertExtension.GetExternName(typeof(object), toType), converted, variable.OwnType());
			return converted;
		}

		public static void Register(PrimitiveConvertersList container, IModulesContainer modules)
		{
			container.AddConverter(new ToPrimitiveConverter());
		}
	}
}