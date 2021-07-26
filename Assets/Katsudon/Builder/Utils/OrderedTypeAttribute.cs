using System;
using System.Collections.Generic;
using Katsudon.Utility;
using UnityEngine.Scripting;

namespace Katsudon.Builder
{
	public static class OrderedTypeUtils
	{
		public static SortedSet<KeyValuePair<T, Type>> GetOrderedSet<T>() where T : OrderedTypeAttributeBase
		{
			return new SortedSet<KeyValuePair<T, Type>>(AttribCollectUtils.CollectTypes<T>(false), new Comparer<T>());
		}

		public struct Comparer<T> : IComparer<KeyValuePair<T, Type>> where T : OrderedTypeAttributeBase
		{
			public int Compare(KeyValuePair<T, Type> x, KeyValuePair<T, Type> y)
			{
				int value = x.Key.registerOrder.CompareTo(y.Key.registerOrder);
				if(value != 0) return value;
				if(x.Value == y.Value) return 0;
				return x.Value.AssemblyQualifiedName.CompareTo(y.Value.AssemblyQualifiedName);
			}
		}
	}

	[AttributeUsage(AttributeTargets.Class, Inherited = true)]
	public abstract class OrderedTypeAttributeBase : PreserveAttribute
	{
		public readonly int registerOrder;

		public OrderedTypeAttributeBase(int registerOrder)
		{
			this.registerOrder = registerOrder;
		}
	}
}