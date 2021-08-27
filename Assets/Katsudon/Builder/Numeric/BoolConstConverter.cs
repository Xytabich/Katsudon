using System;

namespace Katsudon.Builder.Variables
{
	[NumberConverter]
	public class BoolConstConverter : IPrimitiveConverter
	{
		public int order => 80;

		public bool TryConvert(IUdonProgramBlock block, in IVariable variable, Type toType, out IVariable converted)
		{
			if(toType != typeof(bool) || !(variable is IConstVariable constVariable))
			{
				converted = null;
				return false;
			}
			converted = block.machine.GetConstVariable(Convert.ToBoolean(constVariable.value));
			return true;
		}

		public static void Register(PrimitiveConvertersList container, IModulesContainer modules)
		{
			container.AddConverter(new BoolConstConverter());
		}
	}
}