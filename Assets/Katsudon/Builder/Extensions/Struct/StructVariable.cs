using System;
using Katsudon.Builder.Variables;
using Katsudon.Info;

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
					var value = significant.value;
					TryConvert(typeof(object[]), ref value);
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
			var type = value.GetType();
			if(Utils.IsUdonAsmStruct(type))
			{
				var info = AssembliesInfo.instance.GetStructInfo(type);
				var fields = info.fields;
				var instance = new object[FIELDS_OFFSET + fields.Count];
				instance[TYPE_INDEX] = GetStructTypeIdentifier(info.guid);
				for(int i = fields.Count - 1; i >= 0; i--)
				{
					var field = fields[i];
					if(field != null)
					{
						instance[FIELDS_OFFSET + i] = collection.Convert(typeof(object), field.GetValue(value));
					}
				}
				value = instance;
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
			container.AddBuilder(new StructVariable(container));
		}
	}
}