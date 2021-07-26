using System;
using System.Collections.Generic;
using System.Reflection;
using Katsudon.Builder.Helpers;
using Katsudon.Info;

namespace Katsudon.Builder
{
	public class FieldsCollection
	{
		private Dictionary<FieldIdentifier, DeferredValueVariable> fields = new Dictionary<FieldIdentifier, DeferredValueVariable>();

		public FieldsCollection(AsmTypeInfo typeInfo)
		{
			var fields = new Dictionary<FieldIdentifier, AsmFieldInfo>();
			typeInfo.CollectFields(fields);
			foreach(var pair in fields)
			{
				var info = pair.Value;
				DeferredValueVariable variable;
				if(info.syncMode != SyncMode.NotSynced)
				{
					variable = new SyncableVariable(info.name, info.field.FieldType, (info.flags & AsmFieldInfo.Flags.Export) != 0, info.syncMode);
				}
				else
				{
					variable = new DeferredValueVariable(info.name, info.field.FieldType, (info.flags & AsmFieldInfo.Flags.Export) != 0);
				}
				this.fields.Add(pair.Key, variable);
			}
		}

		public DeferredValueVariable GetField(FieldInfo fieldInfo)
		{
			var id = UdonCacheHelper.cache.GetFieldIdentifier(fieldInfo);
			if(fields.TryGetValue(id, out var variable))
			{
				return variable;
			}
			throw new System.Exception("Unknown field: " + fieldInfo);
		}

		public void Apply(PropertiesBlock properties)
		{
			foreach(var pair in fields)
			{
				properties.AddVariable(pair.Value);
			}
		}

		public class DeferredValueVariable : SignificantVariable, IExportableVariable
		{
			public bool export { get; private set; }

			public DeferredValueVariable(string name, Type type, bool export) : base(name, type, null)
			{
				this.export = export;
			}

			public void SetValue(object value)
			{
				this.value = value;
			}
		}

		private class SyncableVariable : DeferredValueVariable, ISyncableVariable
		{
			public SyncMode syncMode { get; private set; }

			public SyncableVariable(string name, Type type, bool export, SyncMode syncMode) : base(name, type, export)
			{
				this.syncMode = syncMode;
			}
		}
	}
}