using System;
using Katsudon.Builder.Externs;

namespace Katsudon.Builder.Extensions.EnumExtension
{
	[NumberConverter]
	public class ToPrimitiveConverter : IFromNumberConverter
	{
		public int order => 80;

		public bool TryConvert(IUdonProgramBlock block, in IVariable variable, Type toType, out IVariable converted)
		{
			var toCode = NumberCodeUtils.GetCode(toType);
			if(!NumberCodeUtils.IsPrimitive(toCode) || Type.GetTypeCode(variable.type) != toCode)
			{
				converted = null;
				return false;
			}

			converted = block.GetTmpVariable(toType);
			block.machine.AddExtern(ConvertExtension.GetExternName(typeof(object), toType), converted, variable.OwnType());
			return true;
		}

		public static void Register(NumericConvertersList container, IModulesContainer modules)
		{
			container.AddConverter(new ToPrimitiveConverter());
		}
	}
}