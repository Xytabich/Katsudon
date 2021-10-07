using System;
using System.Runtime.Serialization;
using Katsudon.Builder.Converters;
using Katsudon.Builder.Variables;

namespace Katsudon.Builder.Extensions.Struct
{
	[VariableBuilder(0)]
	public class StructPrototypeVariable : IVariableBuilder
	{
		int IVariableBuilder.order => 25;

		bool IVariableBuilder.TryBuildVariable(IVariable variable, VariablesTable table)
		{
			var type = variable.type;
			if(type == typeof(StructPrototype))
			{
				if(variable is ISignificantVariable significant && significant.value != null)
				{
					object value = FormatterServices.GetUninitializedObject(((StructPrototype)significant.value).type);
					UdonValueResolver.instance.TryConvertToUdon(value, out var v);
					table.AddVariable(TypedSignificantVariable.From(variable, typeof(object[]), v));
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
			container.AddBuilder(new StructPrototypeVariable());
		}
	}

	public struct StructPrototype : IEquatable<StructPrototype>
	{
		public readonly Type type;

		public StructPrototype(Type type)
		{
			this.type = type;
		}

		public bool Equals(StructPrototype other)
		{
			return other.type == type;
		}

		public override bool Equals(object obj)
		{
			return obj is StructPrototype prototype && prototype.type == type;
		}

		public override int GetHashCode()
		{
			return type.GetHashCode();
		}
	}
}