using System;
using System.Runtime.Serialization;
using Katsudon.Builder.Variables;

namespace Katsudon.Builder.Extensions.Struct
{
	[VariableBuilder(1)]
	public class StructPrototypeVariable : IVariableBuilder
	{
		int IVariableBuilder.order => 25;

		private StructVariable structBuilder;

		public StructPrototypeVariable(StructVariable structBuilder)
		{
			this.structBuilder = structBuilder;
		}

		bool IVariableBuilder.TryBuildVariable(IVariable variable, VariablesTable table)
		{
			var type = variable.type;
			if(type == typeof(StructPrototype))
			{
				if(variable is ISignificantVariable significant && significant.value != null)
				{
					object value = FormatterServices.GetUninitializedObject(((StructPrototype)significant.value).type);
					structBuilder.TryConvert(typeof(object[]), ref value);
					table.AddVariable(TypedSignificantVariable.From(variable, typeof(object[]), value));
				}
				else
				{
					table.AddVariable(TypedVariable.From(variable, typeof(object[])));
				}
				return true;
			}
			return false;
		}

		public bool TryConvert(Type toType, ref object value)
		{
			return false;
		}

		public static void Register(VariableBuildersCollection container, IModulesContainer modules)
		{
			container.AddBuilder(new StructPrototypeVariable(modules.GetModule<StructVariable>()));
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