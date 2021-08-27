using System;
using System.Collections.Generic;
using Katsudon.Builder;
using Katsudon.Editor.Converters;
using Katsudon.Utility;
using NUnit.Framework;

namespace Katsudon.Editor
{
	public class UdonValueResolver : IComparer<IValueConverter>
	{
		private SortedSet<IValueConverter> resolvers;

		public UdonValueResolver()
		{
			resolvers = new SortedSet<IValueConverter>(this);
			
			var sortedTypes = OrderedTypeUtils.GetOrderedSet<ValueConverterAttribute>();
			var args = new object[] { resolvers };
			foreach(var pair in sortedTypes)
			{
				var method = MethodSearch<ValueConverterDelegate>.FindStaticMethod(pair.Value, "Register");
				Assert.IsNotNull(method, string.Format("Value converter with type {0} does not have a Register method", pair.Value));
				method.Invoke(null, args);
			}
		}

		public bool TryConvertToUdon(object value, out object outValue)
		{
			outValue = value;
			if(value == null) return true;
			foreach(var resolver in resolvers)
			{
				if(resolver.TryConvertToUdon(value, out outValue, out bool isAllowed))
				{
					return isAllowed;
				}
			}
			if(Utils.IsUdonType(value.GetType()))
			{
				outValue = value;
				if(value == null) return true;
			}
			return true;
		}

		public bool TryConvertFromUdon(object value, Type type, out object outValue)
		{
			outValue = value;
			if(value == null) return true;
			if(value.GetType() == type)
			{
				return true;
			}
			foreach(var resolver in resolvers)
			{
				if(resolver.TryConvertFromUdon(value, type, out outValue, out bool isAllowed))
				{
					return isAllowed;
				}
			}
			if(Utils.IsUdonType(value.GetType()))
			{
				if(!type.IsAssignableFrom(value.GetType()))
				{
					return false;
				}
				outValue = value;
				return true;
			}
			return false;
		}

		int IComparer<IValueConverter>.Compare(IValueConverter x, IValueConverter y)
		{
			if(x.order == y.order) return x == y ? 0 : -1;
			return x.order.CompareTo(y.order);
		}
	}
}