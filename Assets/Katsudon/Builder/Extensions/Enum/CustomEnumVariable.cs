using Katsudon.Builder.Variables;
using System;

namespace Katsudon.Builder.Extensions.EnumExtension
{
	[VariableBuilder]
	public class CustomEnumVariable : IVariableBuilder
	{
		int IVariableBuilder.order => 25;

		bool IVariableBuilder.TryBuildVariable(IVariable variable, VariablesTable table)
		{
			var type = variable.type;
			if(type.IsEnum && !Utils.IsUdonType(type))
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

		public static void Register(VariableBuildersCollection container, IModulesContainer modules)
		{
			container.AddBuilder(new CustomEnumVariable());
		}
	}
}