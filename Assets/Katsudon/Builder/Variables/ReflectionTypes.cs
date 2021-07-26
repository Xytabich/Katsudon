using System;
using Katsudon.Info;

namespace Katsudon.Builder.Variables
{
	[VariableBuilder]
	public class ReflectionTypes : IVariableBuilder
	{
		int IVariableBuilder.order => 50;

		private AssembliesInfo assemblies;

		private ReflectionTypes(AssembliesInfo assemblies)
		{
			this.assemblies = assemblies;
		}

		bool IVariableBuilder.TryBuildVariable(IVariable variable, VariablesTable table)
		{
			var type = variable.type;
			if(typeof(Type).IsAssignableFrom(type))
			{
				if(variable is ISignificantVariable significant)
				{
					if(Utils.IsUdonAsm((Type)significant.value))
					{
						table.AddVariable(TypedSignificantVariable.From(variable, typeof(Guid), assemblies.GetTypeInfo((Type)significant.value).guid));
					}
					else
					{
						table.AddVariable(TypedSignificantVariable.From(variable, typeof(Type), significant.value));
					}
				}
				else
				{
					table.AddVariable(TypedVariable.From(variable, typeof(object)));
				}
				return true;
			}
			return false;
		}

		bool IVariableBuilder.TryConvert(Type type, ref object value)
		{
			return false;
		}

		public static void Register(VariableBuildersCollection container, IModulesContainer modules)
		{
			container.AddBuilder(new ReflectionTypes(modules.GetModule<AssembliesInfo>()));
		}
	}
}