using System;
using System.Collections.Generic;

namespace Katsudon.Builder.Converters
{
	public interface IValueConverter
	{
		int order { get; }

		Type GetUdonType(Type type);

		bool TryConvertToUdon(object value, out object converted, out bool isAllowed);

		bool TryConvertFromUdon(object value, Type toType, out object converted, out bool isAllowed, ref bool reserialize);
	}

	public delegate void ValueConverterDelegate(UdonValueResolver resolver, ICollection<IValueConverter> container);

	public sealed class ValueConverterAttribute : OrderedTypeAttributeBase
	{
		public ValueConverterAttribute(int registerOrder = 0) : base(registerOrder) { }
	}
}