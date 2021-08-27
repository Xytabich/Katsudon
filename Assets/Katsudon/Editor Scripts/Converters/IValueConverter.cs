using System;
using System.Collections.Generic;
using Katsudon.Builder;

namespace Katsudon.Editor.Converters
{
	public interface IValueConverter
	{
		int order { get; }

		bool TryConvertToUdon(object value, out object converted, out bool isAllowed);

		bool TryConvertFromUdon(object value, Type toType, out object converted, out bool isAllowed);
	}

	public delegate void ValueConverterDelegate(ICollection<IValueConverter> container);

	public sealed class ValueConverterAttribute : OrderedTypeAttributeBase
	{
		public ValueConverterAttribute(int registerOrder = 0) : base(registerOrder) { }
	}
}