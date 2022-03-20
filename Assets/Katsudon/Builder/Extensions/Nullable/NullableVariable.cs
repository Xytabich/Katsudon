using Katsudon.Builder.Variables;
using System;

namespace Katsudon.Builder.Extensions.NullableExtension
{
	[VariableBuilder]
	public class NullableVariable : IVariableBuilder
	{
		int IVariableBuilder.order => 25;

		bool IVariableBuilder.TryBuildVariable(IVariable variable, VariablesTable table)
		{
			var type = variable.type;
			if(type.IsGenericType)
			{
				if(type.GetGenericTypeDefinition() == typeof(Nullable<>))
				{
					if(variable is ISignificantVariable significant && significant.value != null)
					{
						table.AddVariable(TypedSignificantVariable.From(variable, typeof(object), significant.value));
					}
					else
					{
						table.AddVariable(TypedVariable.From(variable, typeof(object)));
					}
					return true;
				}
			}
			return false;
		}

		public static void Register(VariableBuildersCollection container, IModulesContainer modules)
		{
			container.AddBuilder(new NullableVariable());
		}
	}
}