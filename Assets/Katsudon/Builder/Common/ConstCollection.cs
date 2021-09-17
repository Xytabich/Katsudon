using System;
using System.Collections.Generic;

namespace Katsudon.Builder
{
	public class ConstCollection
	{
		private Dictionary<object, ConstVariable> constants = new Dictionary<object, ConstVariable>();
		private IVariable nullConst = null;

		public IVariable GetConstVariable(object value)
		{
			return GetConstVariable(value, value == null ? typeof(object) : value.GetType());
		}

		public IVariable GetConstVariable(object value, Type type)
		{
			if(value == null)
			{
				if(nullConst == null)
				{
					nullConst = new NullConstVariable();
				}
				return nullConst;
			}

			ConstVariable variable;
			if(constants.TryGetValue(value, out variable))
			{
				return variable;
			}
			variable = new ConstVariable(value, type);
			constants[value] = variable;
			return variable;
		}

		public void Apply(PropertiesBlock properties)
		{
			if(nullConst != null) properties.AddVariable(nullConst);
			foreach(var item in constants)
			{
				if(item.Value.isUsed)
				{
					properties.AddVariable(item.Value);
				}
			}
		}

		private class ConstVariable : UnnamedVariable, IConstVariable, ISignificantVariable
		{
			public object value { get; private set; }

			public bool isUsed = false;

			public ConstVariable(object value, Type type) : base("const", type)
			{
				this.value = value;
			}

			public override void Use()
			{
				isUsed = true;
			}
		}

		private class NullConstVariable : UnnamedVariable, ISignificantVariable
		{
			public object value => null;

			public NullConstVariable() : base("null", typeof(object)) { }
		}
	}
}