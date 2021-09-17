using System;

namespace Katsudon.Builder
{
	public class NamedVariable : IVariable
	{
		public string name { get; private set; }
		public Type type { get; private set; }
		public uint address
		{
			get
			{
				if(!_address.HasValue) UnityEngine.Debug.Log("Named variable has no address:" + type + ":" + name);
				return _address.Value;
			}
		}

		private uint? _address = null;

		public NamedVariable(string name, Type type)
		{
			this.name = name;
			this.type = type;
		}

		public void Use() { }

		public void Allocate(int count = 1) { }

		void IVariable.SetAddress(uint address)
		{
			this._address = address;
		}
	}

	public class UnnamedVariable : IVariable, IDeferredVariableName
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
				if(!_address.HasValue) UnityEngine.Debug.Log("Unnamed variable has no address:" + type + ":" + prefix);
				return _address.Value;
			}
		}

		bool IDeferredVariableName.hasName => _name != null;

		protected string prefix = null;
		private uint? _address = null;
		private string _name = null;

		public UnnamedVariable(string prefix, Type type)
		{
			this.prefix = prefix;
			this.type = type;
		}

		public UnnamedVariable(Type type)
		{
			this.type = type;
		}

		public virtual void Use() { }

		public virtual void Allocate(int count = 1) { }

		public override string ToString()
		{
			return string.Format("Unnamed: ({0}) {1}", type, prefix);
		}

		void IDeferredVariableName.Apply(INamePicker namePicker)
		{
			_name = namePicker.PickName(prefix);
		}

		void IVariable.SetAddress(uint address)
		{
			this._address = address;
		}
	}

	public class SignificantVariable : NamedVariable, ISignificantVariable
	{
		public object value { get; protected set; }

		public SignificantVariable(string name, Type type, object value) : base(name, type)
		{
			this.value = value;
		}
	}

	public class UnnamedSignificantVariable : UnnamedVariable, ISignificantVariable
	{
		public object value { get; protected set; }

		public UnnamedSignificantVariable(string prefix, Type type, object value) : base(prefix, type)
		{
			this.value = value;
		}
	}

	public class SelfPointingVariable : UnnamedVariable, ISelfPointingVariable
	{
		public bool isSelf => true;

		public SelfPointingVariable(string name, Type type) : base(name, type) { }
	}

	public class AddressedSignificantVariable : IVariable, ISignificantVariable, IFixedVariableAddress
	{
		public string name { get; private set; }
		public Type type { get; private set; }
		public uint address { get; private set; }
		public object value { get; private set; }

		public AddressedSignificantVariable(string name, Type type, uint address, object value)
		{
			this.name = name;
			this.type = type;
			this.address = address;
			this.value = value;
		}

		public void Use() { }

		public void Allocate(int count = 1) { }

		void IVariable.SetAddress(uint address) { }
	}
}