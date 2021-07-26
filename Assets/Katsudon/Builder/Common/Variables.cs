using System;
using VRC.Udon;

namespace Katsudon.Builder
{
	public interface IVariable : IAddressLabel
	{
		string name { get; }

		Type type { get; }

		/// <summary>
		/// Called when a variable is used by any operator
		/// </summary>
		void Use();

		/// <summary>
		/// Additional number of possible uses.
		/// Use this if the variable is used more than once in the same context.
		/// </summary>
		void Allocate(int count = 1);

		void SetAddress(uint address);
	}

	public interface IExportableVariable : IVariable
	{
		bool export { get; }
	}

	public interface ISyncableVariable : IVariable
	{
		SyncMode syncMode { get; }
	}

	public interface ISignificantVariable : IVariable
	{
		object value { get; }
	}

	public interface ISelfPointingVariable : IVariable
	{
		bool isSelf { get; }
	}

	public interface IConstVariable : IVariable
	{
		object value { get; }
	}

	public interface IFixedVariableAddress : IVariable
	{

	}

	/// <summary>
	/// A value that will only be known at the end of the build.
	/// Can be used in opcodes.
	/// </summary>
	public interface IDeferredValue<out T>
	{
		/// <summary>
		/// Returns the value to which this object will be replaced
		/// </summary>
		T GetValue();
	}

	public interface IDeferredVariableName : IVariable
	{
		bool hasName { get; }

		void Apply(INamePicker namePicker);
	}

	public interface IReferenceVariable : IVariable
	{
		IVariable GetValueVariable();

		void LoadValue(IMethodDescriptor method);

		void StoreValue(IMethodDescriptor method);
	}

	public class NamedVariable : IVariable
	{
		public string name { get; private set; }
		public Type type { get; private set; }
		public uint address => _address.Value;

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
				if(_name == null) throw new InvalidOperationException("Variable has no value");
				return _name;
			}
		}
		public Type type { get; private set; }
		public uint address => _address.Value;

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

	public class ThisVariable : UnnamedVariable, ISelfPointingVariable
	{
		public bool isSelf => true;

		public ThisVariable() : base("this", typeof(UdonBehaviour)) { }
	}
}