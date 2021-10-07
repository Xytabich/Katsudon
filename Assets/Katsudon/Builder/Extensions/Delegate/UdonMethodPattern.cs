using System;
using System.Collections.Generic;
using Katsudon.Builder.Variables;
using Katsudon.Info;

namespace Katsudon.Builder.Extensions.DelegateExtension
{
	[VariableBuilder]
	public class UdonMethodPatternBuilder : IVariableBuilder
	{
		int IVariableBuilder.order => 50;

		bool IVariableBuilder.TryBuildVariable(IVariable variable, VariablesTable table)
		{
			var type = variable.type;
			if(typeof(UdonMethodPattern).IsAssignableFrom(type))
			{
				if(variable is ISignificantVariable significant)
				{
					var info = ((UdonMethodPattern)significant.value).method;
					var pattern = new object[3 + info.parametersName.Length + (string.IsNullOrEmpty(info.returnName) ? 0 : 1)];
					pattern[DelegateUtility.METHOD_NAME_OFFSET] = info.name;
					pattern[DelegateUtility.DELEGATE_TYPE_OFFSET] = (uint)DelegateUtility.TYPE_UDON_BEHAVIOUR;

					string[] parameters = info.parametersName;
					for(int i = 0; i < parameters.Length; i++)
					{
						pattern[DelegateUtility.ARGUMENTS_OFFSET + i] = parameters[i];
					}

					if(!string.IsNullOrEmpty(info.returnName))
					{
						pattern[DelegateUtility.ARGUMENTS_OFFSET + parameters.Length] = info.returnName;
					}

					table.AddVariable(TypedSignificantVariable.From(variable, typeof(object[]), pattern));
				}
				else
				{
					table.AddVariable(TypedVariable.From(variable, typeof(object[])));
				}
				return true;
			}
			return false;
		}

		public static void Register(VariableBuildersCollection container, IModulesContainer modules)
		{
			container.AddBuilder(new UdonMethodPatternBuilder());
		}
	}

	public struct UdonMethodPattern
	{
		public readonly AsmMethodInfo method;

		public UdonMethodPattern(AsmMethodInfo method)
		{
			this.method = method;
		}

		public override bool Equals(object obj)
		{
			return obj is UdonMethodPattern pattern && method == pattern.method;
		}

		public override int GetHashCode()
		{
			return EqualityComparer<AsmMethodInfo>.Default.GetHashCode(method);
		}
	}
}