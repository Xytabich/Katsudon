using System;
using System.Reflection;

namespace Katsudon.Reflection
{
	public struct PropertyAccess<T>
	{
		public T value
		{
			get
			{
				if(!property.CanRead) throw new InvalidOperationException("Cannot read from property");
				return (T)property.GetValue(instance);
			}
			set
			{
				if(!property.CanWrite) throw new InvalidOperationException("Cannot write to property");
				property.SetValue(instance, value);
			}
		}

		private PropertyInfo property;
		private object instance;

		public PropertyAccess(PropertyInfo property, object instance = null)
		{
			this.property = property;
			this.instance = instance;
		}

		public static implicit operator T(PropertyAccess<T> field)
		{
			return field.value;
		}
	}
}