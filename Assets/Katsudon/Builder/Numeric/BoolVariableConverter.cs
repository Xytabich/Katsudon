using System;
using Katsudon.Builder.Externs;

namespace Katsudon.Builder.Variables
{
	[NumberConverter]
	public class BoolVariableConverter : IPrimitiveConverter
	{
		public int order => 81;

		public bool TryConvert(IUdonProgramBlock block, in IVariable variable, Type toType, out IVariable converted)
		{
			if(toType != typeof(bool))
			{
				converted = null;
				return false;
			}
			converted = block.GetTmpVariable(typeof(bool));
			if(variable.type.IsPrimitive)
			{
				block.machine.ConvertExtern(variable, converted);
			}
			else
			{
				block.machine.AddExtern(ConvertExtension.GetExternName(typeof(object), typeof(bool)), converted, variable.OwnType());
			}
			return true;
		}

		public static void Register(PrimitiveConvertersList container, IModulesContainer modules)
		{
			container.AddConverter(new BoolVariableConverter());
		}
	}
}