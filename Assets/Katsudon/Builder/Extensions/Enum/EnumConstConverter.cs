using System;

namespace Katsudon.Builder.Extensions.EnumExtension
{
	[PrimitiveConverter]
	public class EnumConstConverter : IPrimitiveConverter
	{
		public int order => 50;

		public IVariable TryConvert(IUdonProgramBlock block, in IVariable variable, TypeCode fromPrimitive, TypeCode toPrimitive, Type toType)
		{
			if(fromPrimitive == TypeCode.Object) return null;
			if(!toType.IsEnum) return null;
			if(!(variable is IConstVariable constVariable)) return null;

			return block.machine.GetConstVariable(Enum.ToObject(toType, constVariable.value));
		}

		public static void Register(PrimitiveConvertersList container, IModulesContainer modules)
		{
			container.AddConverter(new EnumConstConverter());
		}
	}
}