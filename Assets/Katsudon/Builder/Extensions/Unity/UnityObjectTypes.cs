using System;
using Katsudon.Builder.Variables;

namespace Katsudon.Builder.Extensions.UnityExtensions
{
	[VariableBuilder]
	public class UnityObjectTypes : IVariableBuilder
	{
		int IVariableBuilder.order => 100;

		bool IVariableBuilder.TryBuildVariable(IVariable variable, VariablesTable table)
		{
			var type = variable.type;
			if(typeof(UnityEngine.Object).IsAssignableFrom(type))
			{
				if(Utils.IsUdonType(type)) return false;
				var significant = variable as ISignificantVariable;
				if(significant?.value != null && !Utils.IsUdonType(significant.value.GetType()))
				{
					return false;
				}

				do type = type.BaseType;
				while(!Utils.IsUdonType(type));

				if(significant?.value != null)
				{
					table.AddVariable(TypedSignificantVariable.From(variable, type, significant.value));
				}
				else
				{
					table.AddVariable(TypedVariable.From(variable, type));

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
			container.AddBuilder(new UnityObjectTypes());
		}
	}
}