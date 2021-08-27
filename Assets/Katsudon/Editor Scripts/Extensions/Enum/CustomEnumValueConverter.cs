using System;
using System.Collections.Generic;
using Katsudon.Editor.Converters;

namespace Katsudon.Editor.Extensions.EnumExtension
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

		bool IValueConverter.TryConvertFromUdon(object value, Type toType, out object converted, out bool isAllowed)
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

		public static void Register(ICollection<IValueConverter> container)
		{
			container.Add(new CustomEnumValueConverter());
		}
	}
}