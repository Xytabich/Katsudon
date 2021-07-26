using System;
using System.Reflection;

namespace Katsudon.Reflection
{
	public struct FieldAccess<T>
	{
		public T value
		{
			get
			{
				return (T)field.GetValue(instance);
			}
			set
			{
				if(field.IsInitOnly) throw new InvalidOperationException("Cannot write to read-only field");
				field.SetValue(instance, value);
			}
		}

		private FieldInfo field;
		private object instance;

		public FieldAccess(FieldInfo field, object instance = null)
		{
			this.field = field;
			this.instance = instance;
		}

		public static implicit operator T(FieldAccess<T> field)
		{
			return field.value;
		}
	}
}