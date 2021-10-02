using System;

namespace Katsudon.Builder.Variables
{
	[PrimitiveConverter]
	public class BoolConstConverter : IPrimitiveConverter
	{
		public int order => 80;

		public IVariable TryConvert(IUdonProgramBlock block, in IVariable variable, TypeCode fromPrimitive, TypeCode toPrimitive, Type toType)
		{
			if(toType != typeof(bool) || !(variable is IConstVariable constVariable))
			{
				return null;
			}
			if(fromPrimitive == TypeCode.Object)
			{
				return block.machine.GetConstVariable(constVariable.value != null);
			}
			if(fromPrimitive == TypeCode.Char)
			{
				return block.machine.GetConstVariable(Convert.ToChar(constVariable.value) != '\0');
			}
			return block.machine.GetConstVariable(Convert.ToBoolean(constVariable.value));
		}

		public static void Register(PrimitiveConvertersList container, IModulesContainer modules)
		{
			container.AddConverter(new BoolConstConverter());
		}
	}
}