using System;
using Katsudon.Builder.Variables;

namespace Katsudon.Builder.Extensions.DelegateExtension
{
	[VariableBuilder]
	public class DelegateVariableBuilder : IVariableBuilder
	{
		int IVariableBuilder.order => 50;

		bool IVariableBuilder.TryBuildVariable(IVariable variable, VariablesTable table)
		{
			var type = variable.type;
			if(typeof(Delegate).IsAssignableFrom(type))
			{
				if(variable is ISignificantVariable significant && significant.value != null)
				{
					throw new Exception("Prebuilt delegate instance is not supported");
				}
				else
				{
					table.AddVariable(TypedVariable.From(variable, typeof(object[])));
				}
				return true;
			}
			return false;
		}

		bool IVariableBuilder.TryConvert(Type toType, ref object value)
		{
			return false;
		}

		public static void Register(VariableBuildersCollection container, IModulesContainer modules)
		{
			container.AddBuilder(new DelegateVariableBuilder());
		}
	}
}