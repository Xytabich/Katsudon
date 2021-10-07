using System;
using System.Collections.Generic;
using Katsudon.Builder.Converters;

namespace Katsudon.Builder.Extensions.UdonExtensions
{
	[ValueConverter]
	public class ProxyTypeConverter : IValueConverter
	{
		int IValueConverter.order => 50;

		bool IValueConverter.TryConvertToUdon(object value, out object converted, out bool isAllowed)
		{
			if(value is Type t && !Utils.IsUdonType(t))
			{
				converted = value;
				isAllowed = false;
				return true;
			}
			converted = null;
			isAllowed = false;
			return false;
		}

		bool IValueConverter.TryConvertFromUdon(object value, Type toType, out object converted, out bool isAllowed, ref bool reserialize)
		{
			if(value is Guid && typeof(Type).IsAssignableFrom(toType))
			{
				converted = value;
				isAllowed = false;
				return true;
			}
			converted = null;
			isAllowed = false;
			return false;
		}

		Type IValueConverter.GetUdonType(Type type)
		{
			if(typeof(Type).IsAssignableFrom(type))
			{
				return typeof(Guid);
			}
			return null;
		}

		public static void Register(UdonValueResolver resolver, ICollection<IValueConverter> container)
		{
			container.Add(new ProxyTypeConverter());
		}
	}
}