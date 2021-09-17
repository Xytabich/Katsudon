using System;

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

	public interface IFixedVariableAddress : IVariable { }

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

		void LoadValue(IUdonProgramBlock block);

		void StoreValue(IUdonProgramBlock block);
	}
}