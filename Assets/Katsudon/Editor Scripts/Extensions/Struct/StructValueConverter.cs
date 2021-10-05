using System;
using System.Collections.Generic;
using Katsudon.Editor.Converters;
using Katsudon.Info;

namespace Katsudon.Builder.Extensions.Struct
{
	[ValueConverter]
	public class StructValueConverter : IValueConverter
	{
		int IValueConverter.order => 50;

		private UdonValueResolver resolver;

		public StructValueConverter(UdonValueResolver resolver)
		{
			this.resolver = resolver;
		}

		bool IValueConverter.TryConvertToUdon(object value, out object converted, out bool isAllowed)
		{
			if(Utils.IsUdonAsmStruct(value.GetType()))
			{
				var info = AssembliesInfo.instance.GetStructInfo(value.GetType());
				var fields = info.fields;
				var instance = new object[StructVariable.FIELDS_OFFSET + fields.Count];
				instance[StructVariable.TYPE_INDEX] = StructVariable.GetStructTypeIdentifier(info.guid);
				for(int i = fields.Count - 1; i >= 0; i--)
				{
					if(resolver.TryConvertToUdon(fields[i].GetValue(value), out var c))
					{
						instance[StructVariable.FIELDS_OFFSET + i] = c;
					}
				}
				converted = instance;
				isAllowed = true;
				return true;
			}
			converted = null;
			isAllowed = false;
			return false;
		}

		bool IValueConverter.TryConvertFromUdon(object value, Type toType, out object converted, out bool isAllowed)
		{
			if(value is object[] instance && Utils.IsUdonAsmStruct(toType))
			{
				var info = AssembliesInfo.instance.GetStructInfo(toType);
				var fields = info.fields;
				converted = Activator.CreateInstance(toType, true);
				for(int i = fields.Count - 1; i >= 0; i--)
				{
					var field = fields[i];
					if(resolver.TryConvertFromUdon(instance[StructVariable.FIELDS_OFFSET + i], field.FieldType, out var c))
					{
						field.SetValue(converted, c);
					}
				}
				isAllowed = true;
				return true;
			}
			converted = null;
			isAllowed = false;
			return false;
		}

		public static void Register(UdonValueResolver resolver, ICollection<IValueConverter> container)
		{
			container.Add(new StructValueConverter(resolver));
		}
	}
}