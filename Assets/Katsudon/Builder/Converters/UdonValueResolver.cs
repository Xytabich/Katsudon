using System;
using System.Collections.Generic;
using Katsudon.Utility;
using UnityEngine.Assertions;

namespace Katsudon.Builder.Converters
{
	public class UdonValueResolver : IComparer<IValueConverter>
	{
		private static UdonValueResolver _instance = null;
		public static UdonValueResolver instance => _instance ?? (_instance = new UdonValueResolver());

		private SortedSet<IValueConverter> resolvers;

		private UdonValueResolver()
		{
			resolvers = new SortedSet<IValueConverter>(this);

			var sortedTypes = OrderedTypeUtils.GetOrderedSet<ValueConverterAttribute>();
			var args = new object[] { this, resolvers };
			foreach(var pair in sortedTypes)
			{
				var method = MethodSearch<ValueConverterDelegate>.FindStaticMethod(pair.Value, "Register");
				Assert.IsNotNull(method, string.Format("Value converter with type {0} does not have a Register method", pair.Value));
				method.Invoke(null, args);
			}
		}

		public bool TryGetUdonType(Type type, out Type udonType)//FIX: cache
		{
			var types = CollectionCache.GetList<Type>();
			foreach(var resolver in resolvers)
			{
				var outType = resolver.GetUdonType(type);
				if(outType != null) types.Add(outType);
			}
			if(types.Count == 0)
			{
				if(Utils.IsUdonType(type))
				{
					udonType = type;
				}
				else
				{
					udonType = null;
				}
			}
			else
			{
				if(Utils.IsUdonType(type)) types.Add(type);// The "default" resolver is also involved

				udonType = types[0];
				for(int i = 1; i < types.Count; i++)
				{
					var t = types[i];
					if(t.IsAssignableFrom(udonType))
					{
						udonType = t;
						continue;
					}
					while(!udonType.IsAssignableFrom(t))
					{
						udonType = udonType.BaseType;
					}
				}
				while(!Utils.IsUdonType(udonType))
				{
					udonType = udonType.BaseType;
				}
			}
			CollectionCache.Release(types);
			return udonType != null;
		}

		public bool TryConvertToUdon(object value, out object outValue)//TODO: recursion check
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
				return true;
			}
			return false;
		}

		public bool TryConvertFromUdon(object value, Type type, out object outValue, out bool reserialize)//TODO: recursion check
		{
			outValue = value;
			reserialize = false;
			if(value == null) return true;
			if(value.GetType() == type)
			{
				return true;
			}
			foreach(var resolver in resolvers)
			{
				if(resolver.TryConvertFromUdon(value, type, out outValue, out bool isAllowed, ref reserialize))
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
				reserialize = false;
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