using System;
using Katsudon.Builder.Helpers;
using Katsudon.Builder.Variables;

namespace Katsudon.Builder.Extensions.DelegateExtension
{
	[VariableBuilder]
	public class ExternMethodPatternBuilder : IVariableBuilder
	{
		int IVariableBuilder.order => 50;

		bool IVariableBuilder.TryBuildVariable(IVariable variable, VariablesTable table)
		{
			var type = variable.type;
			if(typeof(ExternMethodPattern).IsAssignableFrom(type))
			{
				if(variable is ISignificantVariable significant)
				{
					var info = ((ExternMethodPattern)significant.value);
					var pattern = new object[3];
					pattern[DelegateUtility.METHOD_NAME_OFFSET] = info.fullName;
					pattern[DelegateUtility.DELEGATE_TYPE_OFFSET] = info.isStatic ? (uint)DelegateUtility.TYPE_STATIC_EXTERN : (uint)DelegateUtility.TYPE_EXTERN;

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
			container.AddBuilder(new ExternMethodPatternBuilder());
		}
	}

	public struct ExternMethodPattern
	{
		public readonly string fullName;
		public readonly bool isStatic;
		private MethodIdentifier method;

		public ExternMethodPattern(string fullName, bool isStatic, MethodIdentifier method)
		{
			this.fullName = fullName;
			this.isStatic = isStatic;
			this.method = method;
		}

		public override bool Equals(object obj)
		{
			return obj is ExternMethodPattern pattern && method == pattern.method;
		}

		public override int GetHashCode()
		{
			return method.GetHashCode();
		}
	}
}