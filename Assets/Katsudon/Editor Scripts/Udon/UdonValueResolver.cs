using System;
using System.Collections.Generic;
using Katsudon.Editor.Converters;

namespace Katsudon.Editor
{
	public class UdonValueResolver : IComparer<IValueConverter>
	{
		private SortedSet<IValueConverter> resolvers;

		public UdonValueResolver()
		{
			resolvers = new SortedSet<IValueConverter>(this);
			resolvers.Add(new BehaviourTypes());
		}

		public object ConvertToUdon(object value)
		{
			if(value == null) return null;
			foreach(var resolver in resolvers)
			{
				if(resolver.TryConvertToUdon(value, out var outValue)) return outValue;
			}
			if(Utils.IsUdonType(value.GetType()))
			{
				return value;
			}
			throw new Exception("Type is not supported: " + value.GetType());
		}

		public object ConvertFromUdon(object value, Type type)
		{
			if(value == null) return null;
			if(value.GetType() == type)
			{
				return value;
			}
			foreach(var resolver in resolvers)
			{
				if(resolver.TryConvertFromUdon(value, type, out var outValue)) return outValue;
			}
			if(Utils.IsUdonType(value.GetType()))
			{
				if(!type.IsAssignableFrom(value.GetType()))
				{
					throw new Exception(string.Format("{0} cannot be converted to {1}", value.GetType(), type));
				}
				return value;
			}
			throw new Exception("Type is not supported: " + type);
		}

		int IComparer<IValueConverter>.Compare(IValueConverter x, IValueConverter y)
		{
			if(x.order == y.order) return x == y ? 0 : -1;
			return x.order.CompareTo(y.order);
		}
	}
}