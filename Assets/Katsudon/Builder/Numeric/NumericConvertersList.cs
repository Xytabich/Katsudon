using System;
using System.Collections.Generic;
using Katsudon.Utility;
using UnityEngine.Assertions;

namespace Katsudon.Builder
{
	public class NumericConvertersList : IComparer<IFromNumberConverter>
	{
		private SortedSet<IFromNumberConverter> converters;

		public NumericConvertersList(IModulesContainer modules)
		{
			converters = new SortedSet<IFromNumberConverter>(this);
			modules.AddModule(this);

			var sortedTypes = OrderedTypeUtils.GetOrderedSet<NumberConverterAttribute>();
			var args = new object[] { this, modules };
			foreach(var pair in sortedTypes)
			{
				var method = MethodSearch<NumberConverterDelegate>.FindStaticMethod(pair.Value, "Register");
				Assert.IsNotNull(method, string.Format("Number converter with type {0} does not have a Register method", pair.Value));
				method.Invoke(null, args);
			}
		}

		public void AddConverter(IFromNumberConverter converter)
		{
			converters.Add(converter);
		}

		/// <summary>
		/// Tries to convert the original numeric value to any other type.
		/// Constants are converted immediately, but udon code is built for variables. 
		/// </summary>
		public bool TryConvert(IMachineBlock block, IVariable value, Type toType, out IVariable converted)
		{
			if(value.type == toType)
			{
				converted = value;
				return true;
			}
			foreach(var converter in converters)
			{
				if(converter.TryConvert(block, value, toType, out converted))
				{
					return true;
				}
			}
			converted = null;
			return false;
		}

		int IComparer<IFromNumberConverter>.Compare(IFromNumberConverter x, IFromNumberConverter y)
		{
			if(x.order == y.order) return x == y ? 0 : -1;
			return x.order.CompareTo(y.order);
		}
	}

	public interface IFromNumberConverter
	{
		int order { get; }

		bool TryConvert(IMachineBlock block, in IVariable value, Type toType, out IVariable converted);
	}

	public delegate void NumberConverterDelegate(NumericConvertersList container, IModulesContainer modules);

	public sealed class NumberConverterAttribute : OrderedTypeAttributeBase
	{
		public NumberConverterAttribute(int registerOrder = 0) : base(registerOrder) { }
	}
}