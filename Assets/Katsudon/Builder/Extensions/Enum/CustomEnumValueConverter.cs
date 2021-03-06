using System;
using System.Collections.Generic;
using Katsudon.Builder.Converters;

namespace Katsudon.Builder.Extensions.EnumExtension
{
	[ValueConverter]
	public class CustomEnumValueConverter : IValueConverter
	{
		int IValueConverter.order => 50;

		bool IValueConverter.TryConvertToUdon(object value, out object converted, out bool isAllowed)
		{
			if(value is Enum e && !Utils.IsUdonType(e.GetType()))
			{
				converted = Convert.ChangeType(e, Enum.GetUnderlyingType(e.GetType()));
				isAllowed = true;
				return true;
			}
			converted = null;
			isAllowed = false;
			return false;
		}

		bool IValueConverter.TryConvertFromUdon(object value, Type toType, out object converted, out bool isAllowed, ref bool reserialize)
		{
			if(toType.IsEnum && !Utils.IsUdonType(toType))
			{
				converted = Enum.ToObject(toType, value);
				isAllowed = true;
				return true;
			}
			converted = null;
			isAllowed = false;
			return false;
		}

		Type IValueConverter.GetUdonType(Type type)
		{
			if(type.IsEnum && !Utils.IsUdonType(type))
			{
				return Enum.GetUnderlyingType(type);
			}
			return null;
		}

		public static void Register(UdonValueResolver resolver, ICollection<IValueConverter> container)
		{
			container.Add(new CustomEnumValueConverter());
		}
	}
}