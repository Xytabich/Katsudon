using System;

namespace Katsudon.Builder.Variables
{
	[VariableBuilder]
	public class CustomEnumVariable : IVariableBuilder
	{
		int IVariableBuilder.order => 25;

		bool IVariableBuilder.TryBuildVariable(IVariable variable, VariablesTable table)
		{
			var type = variable.type;
			if(typeof(Enum).IsAssignableFrom(type) && !Utils.IsUdonType(type))
			{
				if(variable is ISignificantVariable significant && significant.value != null)
				{
					table.AddVariable(TypedSignificantVariable.From(variable, Enum.GetUnderlyingType(type), significant.value));
				}
				else
				{
					table.AddVariable(TypedVariable.From(variable, Enum.GetUnderlyingType(type)));
				}
				return true;
			}
			return false;
		}

		bool IVariableBuilder.TryConvert(Type type, ref object value)
		{
			if(typeof(Enum).IsAssignableFrom(type))
			{
				if(!Utils.IsUdonType(type) && Enum.GetUnderlyingType(type) == value.GetType())
				{
					return true;
				}
			}
			return false;
		}

		public static void Register(VariableBuildersCollection container, IModulesContainer modules)
		{
			container.AddBuilder(new CustomEnumVariable());
		}
	}
}