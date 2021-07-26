using System;

namespace Katsudon.Editor.Converters
{
	public interface IValueConverter
	{
		int order { get; }

		bool TryConvertToUdon(object value, out object converted);

		bool TryConvertFromUdon(object value, Type toType, out object converted);
	}
}