using System;

namespace Katsudon.Builder.Extensions.EnumExtension
{
	[NumberConverter]
	public class CustomEnumConverter : IFromNumberConverter
	{
		public int order => 50;

		public bool TryConvert(IUdonProgramBlock block, in IVariable variable, Type toType, out IVariable converted)
		{
			if(typeof(Enum).IsAssignableFrom(toType))
			{
				if(!Utils.IsUdonType(toType) && Enum.GetUnderlyingType(toType) == variable.type)
				{
					converted = variable;
					return true;
				}
			}
			if(typeof(Enum).IsAssignableFrom(variable.type))
			{
				if(!Utils.IsUdonType(variable.type) && Enum.GetUnderlyingType(variable.type) == toType)
				{
					converted = variable;
					return true;
				}
			}
			converted = null;
			return false;
		}

		public static void Register(NumericConvertersList container, IModulesContainer modules)
		{
			container.AddConverter(new CustomEnumConverter());
		}
	}
}