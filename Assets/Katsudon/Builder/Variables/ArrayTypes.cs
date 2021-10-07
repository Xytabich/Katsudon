using Katsudon.Builder.Converters;

namespace Katsudon.Builder.Variables
{
	[VariableBuilder]
	public class ArrayTypes : IVariableBuilder
	{
		int IVariableBuilder.order => 150;

		private VariableBuildersCollection collection;

		private ArrayTypes(VariableBuildersCollection collection)
		{
			this.collection = collection;
		}

		bool IVariableBuilder.TryBuildVariable(IVariable variable, VariablesTable table)
		{
			var type = variable.type;
			if(type.IsArray && !Utils.IsUdonType(type))
			{
				if(UdonValueResolver.instance.TryGetUdonType(type, out var udonType))
				{
					if(variable is ISignificantVariable significant && significant.value != null &&
						UdonValueResolver.instance.TryConvertToUdon(significant.value, out var value))
					{
						table.AddVariable(TypedSignificantVariable.From(variable, udonType, value));
					}
					else
					{
						table.AddVariable(TypedVariable.From(variable, udonType));
					}
					return true;
				}
			}
			return false;
		}

		public static void Register(VariableBuildersCollection container, IModulesContainer modules)
		{
			container.AddBuilder(new ArrayTypes(container));
		}
	}
}