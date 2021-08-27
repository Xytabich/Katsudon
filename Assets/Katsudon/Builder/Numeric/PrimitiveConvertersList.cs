using System;
using System.Collections.Generic;
using Katsudon.Utility;
using UnityEngine.Assertions;

namespace Katsudon.Builder
{
	public class PrimitiveConvertersList : IComparer<IPrimitiveConverter>
	{
		private SortedSet<IPrimitiveConverter> converters;

		public PrimitiveConvertersList(IModulesContainer modules)
		{
			converters = new SortedSet<IPrimitiveConverter>(this);
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

		public void AddConverter(IPrimitiveConverter converter)
		{
			converters.Add(converter);
		}

		/// <summary>
		/// Tries to convert the original numeric value to any other type.
		/// Constants are converted immediately, but udon code is built for variables. 
		/// </summary>
		public bool TryConvert(IUdonProgramBlock block, IVariable value, Type toType, out IVariable converted)
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

		int IComparer<IPrimitiveConverter>.Compare(IPrimitiveConverter x, IPrimitiveConverter y)
		{
			if(x.order == y.order) return x == y ? 0 : -1;
			return x.order.CompareTo(y.order);
		}
	}

	public interface IPrimitiveConverter
	{
		int order { get; }

		bool TryConvert(IUdonProgramBlock block, in IVariable value, Type toType, out IVariable converted);
	}

	public delegate void NumberConverterDelegate(PrimitiveConvertersList container, IModulesContainer modules);

	public sealed class NumberConverterAttribute : OrderedTypeAttributeBase
	{
		public NumberConverterAttribute(int registerOrder = 0) : base(registerOrder) { }
	}
}