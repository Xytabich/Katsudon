using System;
using System.Collections.Generic;
using Katsudon.Builder.Variables;
using Katsudon.Info;

namespace Katsudon.Builder.Extensions.DelegateExtension
{
	[VariableBuilder]
	public class MethodPatternBuilder : IVariableBuilder
	{
		private const int METHOD_NAME_OFFSET = 1;
		private const int ARGUMENTS_OFFSET = 2;

		int IVariableBuilder.order => 50;

		bool IVariableBuilder.TryBuildVariable(IVariable variable, VariablesTable table)
		{
			var type = variable.type;
			if(typeof(MethodPattern).IsAssignableFrom(type))
			{
				if(variable is ISignificantVariable significant)
				{
					var info = ((MethodPattern)significant.value).method;
					var pattern = new object[2 + info.parametersName.Length + (string.IsNullOrEmpty(info.returnName) ? 0 : 1)];
					pattern[METHOD_NAME_OFFSET] = info.name;

					string[] parameters = info.parametersName;
					for(int i = 0; i < parameters.Length; i++)
					{
						pattern[ARGUMENTS_OFFSET + i] = parameters[i];
					}

					if(!string.IsNullOrEmpty(info.returnName))
					{
						pattern[ARGUMENTS_OFFSET + parameters.Length] = info.returnName;
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

		bool IVariableBuilder.TryConvert(Type type, ref object value)
		{
			return false;
		}

		public static void Register(VariableBuildersCollection container, IModulesContainer modules)
		{
			container.AddBuilder(new MethodPatternBuilder());
		}
	}

	public struct MethodPattern
	{
		public readonly AsmMethodInfo method;

		public MethodPattern(AsmMethodInfo method)
		{
			this.method = method;
		}

		public override bool Equals(object obj)
		{
			return obj is MethodPattern pattern && method == pattern.method;
		}

		public override int GetHashCode()
		{
			return EqualityComparer<AsmMethodInfo>.Default.GetHashCode(method);
		}
	}
}