using System;

namespace Katsudon.Builder.Variables
{
	public class TypedVariable : IVariable
	{
		public string name => source.name;

		public Type type { get; private set; }

		public uint address { get; private set; }

		private IVariable source;

		protected TypedVariable(IVariable source, Type type)
		{
			this.source = source;
			this.type = type;
		}

		public void Allocate(int count = 1) { }

		public void Use() { }

		public void SetAddress(uint address)
		{
			this.address = address;
			source.SetAddress(address);
		}

		public static TypedVariable From(IVariable variable, Type type)
		{
			switch(GetVariableType(variable))
			{
				case VariableType.Exportable: return new TypedExportableVariable(variable, type);
				case VariableType.Syncable: return new TypedSyncableVariable(variable, type);
				case VariableType.Exportable | VariableType.Syncable: return new TypedSyncableExportableVariable(variable, type);
				default: return new TypedVariable(variable, type);
			}
		}

		protected static VariableType GetVariableType(IVariable variable)
		{
			VariableType type = VariableType.Simple;
			if(variable is IExportableVariable)
			{
				type |= VariableType.Exportable;
			}
			if(variable is ISyncableVariable)
			{
				type |= VariableType.Syncable;
			}
			return type;
		}

		protected enum VariableType
		{
			Simple = 0x00,
			Exportable = 0x01,
			Syncable = 0x02,
			SelfPointing = 0x04
		}
	}

	public class TypedSignificantVariable : TypedVariable, ISignificantVariable
	{
		public object value { get; private set; }

		protected TypedSignificantVariable(IVariable source, Type type, object value) : base(source, type)
		{
			this.value = value;
		}

		public static TypedSignificantVariable From(IVariable variable, Type type, object value)
		{
			switch(GetVariableType(variable))
			{
				case VariableType.Exportable: return new TypedExportableSignificantVariable(variable, type, value);
				case VariableType.Syncable: return new TypedSyncableSignificantVariable(variable, type, value);
				case VariableType.Exportable | VariableType.Syncable: return new TypedSyncableExportableSignificantVariable(variable, type, value);
				default: return new TypedSignificantVariable(variable, type, value);
			}
		}
	}

	public class TypedSyncableVariable : TypedVariable, ISyncableVariable
	{
		public SyncMode syncMode { get; private set; }

		public TypedSyncableVariable(IVariable source, Type type) : base(source, type)
		{
			this.syncMode = (source as ISyncableVariable).syncMode;
		}
	}

	public class TypedExportableVariable : TypedVariable, IExportableVariable
	{
		public bool export { get; private set; }

		public TypedExportableVariable(IVariable source, Type type) : base(source, type)
		{
			this.export = (source as IExportableVariable).export;
		}
	}

	public class TypedSyncableExportableVariable : TypedVariable, IExportableVariable
	{
		public SyncMode syncMode { get; private set; }
		public bool export { get; private set; }

		public TypedSyncableExportableVariable(IVariable source, Type type) : base(source, type)
		{
			this.export = (source as IExportableVariable).export;
			this.syncMode = (source as ISyncableVariable).syncMode;
		}
	}

	public class TypedSyncableSignificantVariable : TypedSignificantVariable, ISyncableVariable
	{
		public SyncMode syncMode { get; private set; }

		public TypedSyncableSignificantVariable(IVariable source, Type type, object value) : base(source, type, value)
		{
			this.syncMode = (source as ISyncableVariable).syncMode;
		}
	}

	public class TypedExportableSignificantVariable : TypedSignificantVariable, IExportableVariable
	{
		public bool export { get; private set; }

		public TypedExportableSignificantVariable(IVariable source, Type type, object value) : base(source, type, value)
		{
			this.export = (source as IExportableVariable).export;
		}
	}

	public class TypedSyncableExportableSignificantVariable : TypedSignificantVariable, IExportableVariable
	{
		public SyncMode syncMode { get; private set; }
		public bool export { get; private set; }

		public TypedSyncableExportableSignificantVariable(IVariable source, Type type, object value) : base(source, type, value)
		{
			this.export = (source as IExportableVariable).export;
			this.syncMode = (source as ISyncableVariable).syncMode;
		}
	}
}