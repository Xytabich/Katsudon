using System;
using System.Collections.Generic;
using Katsudon.Builder.Helpers;
using Katsudon.Utility;
using UnityEngine.Assertions;

namespace Katsudon.Builder.Variables
{
	public class VariableBuildersCollection : IComparer<IVariableBuilder>
	{
		private SortedSet<IVariableBuilder> builders;
		private IReadOnlyDictionary<Type, string> udonTypes;

		public VariableBuildersCollection(IModulesContainer modules)
		{
			builders = new SortedSet<IVariableBuilder>(this);
			udonTypes = UdonCacheHelper.cache.GetTypeNames();
			modules.AddModule(this);

			var sortedTypes = OrderedTypeUtils.GetOrderedSet<VariableBuilderAttribute>();
			var args = new object[] { this, modules };
			foreach(var pair in sortedTypes)
			{
				var method = MethodSearch<VariableBuilderDelegate>.FindStaticMethod(pair.Value, "Register");
				Assert.IsNotNull(method, string.Format("Variable builder with type {0} does not have a Register method", pair.Value));
				method.Invoke(null, args);
			}
		}

		public void AddBuilder(IVariableBuilder builder)
		{
			builders.Add(builder);
		}

		public bool TryBuildVariable(IVariable variable, VariablesTable table)
		{
			foreach(var builder in builders)
			{
				if(builder.TryBuildVariable(variable, table))
				{
					return true;
				}
			}
			return false;
		}

		public object Convert(Type toType, object value)
		{
			if(value == null) return null;

			foreach(var builder in builders)
			{
				if(builder.TryConvert(toType, ref value)) break;
			}
			if(!udonTypes.ContainsKey(value.GetType()))
			{
				throw new InvalidOperationException(string.Format("Type {0} is not supported by Udon", value.GetType()));
			}
			return value;
		}

		int IComparer<IVariableBuilder>.Compare(IVariableBuilder x, IVariableBuilder y)
		{
			if(x.order == y.order) return x == y ? 0 : -1;
			return x.order.CompareTo(y.order);
		}
	}

	public delegate void VariableBuilderDelegate(VariableBuildersCollection container, IModulesContainer modules);

	public sealed class VariableBuilderAttribute : OrderedTypeAttributeBase
	{
		public VariableBuilderAttribute(int registerOrder = 0) : base(registerOrder) { }
	}
}