using System;
using Katsudon.Builder.Converters;
using Katsudon.Builder.Variables;

namespace Katsudon.Builder.Extensions.Struct
{
	[VariableBuilder]
	public class StructVariable : IVariableBuilder
	{
		public const int TYPE_INDEX = 0;
		public const int FIELDS_OFFSET = 1;

		int IVariableBuilder.order => 25;

		private VariableBuildersCollection collection;

		private StructVariable(VariableBuildersCollection collection)
		{
			this.collection = collection;
		}

		bool IVariableBuilder.TryBuildVariable(IVariable variable, VariablesTable table)
		{
			var type = variable.type;
			if(Utils.IsUdonAsmStruct(type))
			{
				if(variable is ISignificantVariable significant && significant.value != null)
				{
					UdonValueResolver.instance.TryConvertToUdon(significant.value, out var value);
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

		public static string GetStructTypeIdentifier(Guid guid)
		{
			return string.Format("{{KatsudonStructType:{0}}}", guid);
		}

		public static void Register(VariableBuildersCollection container, IModulesContainer modules)
		{
			var builder = new StructVariable(container);
			container.AddBuilder(builder);
			modules.AddModule(builder);
		}
	}
}