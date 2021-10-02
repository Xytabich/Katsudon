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

			var sortedTypes = OrderedTypeUtils.GetOrderedSet<PrimitiveConverterAttribute>();
			var args = new object[] { this, modules };
			foreach(var pair in sortedTypes)
			{
				var method = MethodSearch<PrimitiveConverterDelegate>.FindStaticMethod(pair.Value, "Register");
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
		public bool TryConvert(IUdonProgramBlock block, IVariable variable, Type toType, out IVariable converted)
		{
			converted = variable;
			if(variable.type == toType) return true;

			var toCode = Type.GetTypeCode(toType);
			if(!NumberCodeUtils.IsPrimitiveInteger(toCode))
			{
				return false;
			}

			var fromCode = Type.GetTypeCode(variable.type);
			if(!NumberCodeUtils.IsPrimitiveInteger(fromCode))
			{
				if(variable.type.IsValueType) return false;

				fromCode = TypeCode.Object;// nullable object
			}

			foreach(var converter in converters)
			{
				converted = converter.TryConvert(block, variable, fromCode, toCode, toType);
				if(converted != null)
				{
					return true;
				}
			}
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

		IVariable TryConvert(IUdonProgramBlock block, in IVariable fromVariable, TypeCode fromPrimitive, TypeCode toPrimitive, Type toType);
	}

	public delegate void PrimitiveConverterDelegate(PrimitiveConvertersList container, IModulesContainer modules);

	public sealed class PrimitiveConverterAttribute : OrderedTypeAttributeBase
	{
		public PrimitiveConverterAttribute(int registerOrder = 0) : base(registerOrder) { }
	}
}