using System;
using System.Collections.Generic;
using Katsudon.Builder.Converters;

namespace Katsudon.Builder.Extensions.NullableExtension
{
	[ValueConverter]
	public class NullableValueConverter : IValueConverter
	{
		int IValueConverter.order => 50;

		bool IValueConverter.TryConvertToUdon(object value, out object converted, out bool isAllowed)
		{
			var type = value.GetType();
			if(type.IsGenericType)
			{
				if(type.GetGenericTypeDefinition() == typeof(Nullable<>))
				{
					if((bool)type.GetProperty(nameof(Nullable<int>.HasValue)).GetValue(value))
					{
						converted = type.GetProperty(nameof(Nullable<int>.Value)).GetValue(value);
					}
					else
					{
						converted = null;
					}
					isAllowed = true;
					return true;
				}
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
			if(type.IsGenericType && !type.IsGenericTypeDefinition)
			{
				if(type.GetGenericTypeDefinition() == typeof(Nullable<>))
				{
					return typeof(object);
				}
			}
			return null;
		}

		public static void Register(UdonValueResolver resolver, ICollection<IValueConverter> container)
		{
			container.Add(new NullableValueConverter());
		}
	}
}