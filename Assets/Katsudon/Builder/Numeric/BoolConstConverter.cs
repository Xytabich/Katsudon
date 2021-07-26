using System;

namespace Katsudon.Builder.Variables
{
	[NumberConverter]
	public class BoolConstConverter : IFromNumberConverter
	{
		public int order => 80;

		public bool TryConvert(IMethodDescriptor method, in IVariable variable, Type toType, out IVariable converted)
		{
			if(toType != typeof(bool) || !(variable is IConstVariable constVariable))
			{
				converted = null;
				return false;
			}
			converted = method.machine.GetConstVariable(Convert.ToBoolean(constVariable.value));
			return true;
		}

		public static void Register(NumericConvertersList container, IModulesContainer modules)
		{
			container.AddConverter(new BoolConstConverter());
		}
	}
}