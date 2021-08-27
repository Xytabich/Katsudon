using System;

namespace Katsudon.Builder.Extensions.EnumExtension
{
	[NumberConverter]
	public class EnumConstConverter : IPrimitiveConverter
	{
		public int order => 50;

		public bool TryConvert(IUdonProgramBlock block, in IVariable variable, Type toType, out IVariable converted)
		{
			if(!(variable is IConstVariable constVariable))
			{
				converted = null;
				return false;
			}
			if(!typeof(Enum).IsAssignableFrom(toType))
			{
				converted = null;
				return false;
			}

			converted = block.machine.GetConstVariable(Enum.ToObject(toType, constVariable.value));
			return true;
		}

		public static void Register(PrimitiveConvertersList container, IModulesContainer modules)
		{
			container.AddConverter(new EnumConstConverter());
		}
	}
}