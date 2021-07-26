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

		private class ConstVariable : IVariable, IConstVariable, ISignificantVariable, IDeferredVariableName
		{
			public string name
			{
				get
				{
					if(_name == null) throw new InvalidOperationException("Variable has no name");
					return _name;
				}
			}

			public Type type { get; private set; }

			public uint address
			{
				get
				{
					if(!_address.HasValue) UnityEngine.Debug.Log("Const has no address:" + type + ":" + value);
					return _address.Value;
				}
			}

			public object value { get; private set; }

			public bool isUsed = false;

			bool IDeferredVariableName.hasName => _name != null;

			private uint? _address = null;
			private string _name = null;

			public ConstVariable(object value, Type type)
			{
				this.type = type;
				this.value = value;
			}

			public void Use()
			{
				isUsed = true;
			}

			public void Allocate(int count = 1) { }

			public void Apply(INamePicker namePicker)
			{
				_name = namePicker.PickName("const");
			}

			void IVariable.SetAddress(uint address)
			{
				this._address = address;
			}
		}

		private class NullConstVariable : IVariable, ISignificantVariable, IDeferredVariableName
		{
			public string name
			{
				get
				{
					if(_name == null) throw new InvalidOperationException("Null const has no name");
					return _name;
				}
			}

			public Type type => typeof(object);

			public object value => null;

			public uint address
			{
				get
				{
					if(!_address.HasValue) UnityEngine.Debug.Log("Null const has no address");
					return _address.Value;
				}
			}


			bool IDeferredVariableName.hasName => _name != null;

			private uint? _address = null;
			private string _name = null;

			public void Use() { }

			public void Allocate(int count = 1) { }

			public void Apply(INamePicker namePicker)
			{
				_name = namePicker.PickName("null");
			}

			void IVariable.SetAddress(uint address)
			{
				this._address = address;
			}
		}
	}
}