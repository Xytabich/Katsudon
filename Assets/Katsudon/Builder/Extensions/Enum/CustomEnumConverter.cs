using System;

namespace Katsudon.Builder.Extensions.EnumExtension
{
	[PrimitiveConverter]
	public class CustomEnumConverter : IPrimitiveConverter
	{
		public int order => 50;

		public IVariable TryConvert(IUdonProgramBlock block, in IVariable variable, TypeCode fromPrimitive, TypeCode toPrimitive, Type toType)
		{
			if(fromPrimitive == TypeCode.Object) return null;
			if(toType.IsEnum)
			{
				if(!variable.type.IsPrimitive) return null;
				if(!Utils.IsUdonType(toType) && Enum.GetUnderlyingType(toType) == variable.type)
				{
					return variable;
				}
			}
			if(variable.type.IsEnum)
			{
				if(!Utils.IsUdonType(variable.type) && Enum.GetUnderlyingType(variable.type) == toType)
				{
					return variable;
				}
			}
			return null;
		}

		public static void Register(PrimitiveConvertersList container, IModulesContainer modules)
		{
			container.AddConverter(new CustomEnumConverter());
		}
	}
}