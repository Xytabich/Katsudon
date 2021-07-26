using System;
using System.Collections.Generic;
using System.Text;
using Katsudon.Builder.Helpers;
using Katsudon.Builder.Variables;
using Katsudon.Info;
using VRC.Udon.Common.Interfaces;

namespace Katsudon.Builder
{
	public class PropertiesBlock : IUAssemblyBlock, INamePicker
	{
		int IUAssemblyBlock.order => 0;

		private List<IVariable> variables = new List<IVariable>();
		private VariablesTable variablesTable;
		private Dictionary<string, int> namesCounter;

		private IReadOnlyDictionary<Type, string> typeNames;
		private VariableBuildersCollection collection;

		public PropertiesBlock(VariableBuildersCollection buidersCollection, Dictionary<string, int> namesCounter)
		{
			this.collection = buidersCollection;
			this.namesCounter = namesCounter;
		}

		public void AddVariable(IVariable variable)
		{
			typeNames = UdonCacheHelper.cache.GetTypeNames();
			variables.Add(variable);
		}

		public string PickName(Type type, string prefix = null)
		{
			return PickName(string.IsNullOrEmpty(prefix) ? Utils.PrepareMemberName(type.ToString()) :
				string.Format("{0}_{1}", prefix, Utils.PrepareMemberName(type.ToString())));
		}

		public string PickName(string baseName)
		{
			int count;
			if(!namesCounter.TryGetValue(baseName, out count))
			{
				count = 0;
			}
			var name = string.Format(AsmTypeInfo.INTERNAL_NAME_FORMAT, count, baseName);
			count++;
			namesCounter[baseName] = count;
			return name;
		}

		void IUAssemblyBlock.AppendCode(StringBuilder builder)
		{
			variablesTable = new VariablesTable(this, variables.Count);

			var deferredVariables = new List<IVariable>(variables.Count);
			foreach(var variable in variables)
			{
				if(variable is IFixedVariableAddress)
				{
					variablesTable.SetFixed(variable.address);
					AddVariable(variable, variablesTable);
				}
				else
				{
					deferredVariables.Add(variable);
				}
			}

			for(var i = 0; i < deferredVariables.Count; i++)
			{
				AddVariable(deferredVariables[i], variablesTable);
			}

			int variablesCount;
			var addVariables = variablesTable.GetVariables(out variablesCount);

			builder.Append(".data_start\n");
			for(var i = 0; i < variablesCount; i++)
			{
				if((addVariables[i] is IExportableVariable exportable) && exportable.export)
				{
					builder.Append('\t');
					builder.Append(".export ");
					builder.Append(exportable.name);
					builder.Append('\n');
				}
			}

			for(var i = 0; i < variablesCount; i++)
			{
				if((addVariables[i] is ISyncableVariable syncable) && syncable.syncMode != SyncMode.NotSynced)
				{
					builder.Append('\t');
					builder.Append(".sync ");
					builder.Append(syncable.name);
					builder.Append(", ");
					builder.Append(syncable.syncMode.ToString().ToLowerInvariant());
					builder.Append('\n');
				}
			}

			for(var i = 0; i < variablesCount; i++)
			{
				var variable = addVariables[i];
				builder.Append('\t');
				builder.Append(variable.name);
				builder.Append(": %");
				builder.Append(typeNames[variable.type]);
				builder.Append(", ");
				builder.Append(((variable is ISelfPointingVariable selfPointer) && selfPointer.isSelf) ? "this" : "null");
				builder.Append('\n');
			}
			builder.Append(".data_end\n");
		}

		void IUAssemblyBlock.InitProgram(IUdonProgram program)
		{
			int count;
			var variables = variablesTable.GetVariables(out count);
			for(var i = 0; i < count; i++)
			{
				var variable = variables[i];
				if((variable is ISignificantVariable significant) && significant.value != null)
				{
					program.Heap.SetHeapVariable(variable.address, significant.value, variable.type);
				}
			}
		}

		private void AddVariable(IVariable variable, VariablesTable table)
		{
			if(variable is IDeferredValue<IVariable>)
			{
				variable = (variable as IDeferredValue<IVariable>).GetValue();
			}
			if(variable is IDeferredVariableName deferred && !deferred.hasName)
			{
				deferred.Apply(this);
			}
			if(!collection.TryBuildVariable(variable, table))
			{
				table.AddVariable(variable);
			}
		}
	}

	public interface INamePicker
	{
		string PickName(Type type, string prefix = null);

		string PickName(string baseName);
	}

	public class VariablesTable
	{
		private PropertiesBlock properties;
		private uint addressCounter = 0;
		private IVariable[] variables;
		private HashSet<string> names = new HashSet<string>();

		private bool isFixed;
		private uint fixedAddress;

		public VariablesTable(PropertiesBlock properties, int capacity)
		{
			this.properties = properties;
			variables = new IVariable[capacity];
		}

		public void SetFixed(uint address)
		{
			isFixed = true;
			fixedAddress = address;
		}

		public void AddVariable(IVariable variable)
		{
			if(variable is IDeferredVariableName deferred && !deferred.hasName)
			{
				deferred.Apply(properties);
			}
			if(!names.Add(variable.name))
			{
				throw new Exception(string.Format("Variable with name {0} already exists", variable.name));
			}
			if(!Utils.IsUdonType(variable.type))
			{
				throw new InvalidOperationException(string.Format("Type {0} is not supported by Udon", variable.type));
			}
			if(variable is IFixedVariableAddress)
			{
				SetToAddress(variable, variable.address);
			}
			else
			{
				AddNewAddress(variable);
			}
			isFixed = false;
		}

		private void SetToAddress(IVariable variable, uint address)
		{
			AllocateAddress(address);
			if(variables[address] != null)
			{
				throw new Exception(string.Format("The given address {0} already contains the variable {1}", address, variables[address]));
			}
			variables[address] = variable;
			if(addressCounter == address) addressCounter++;
		}

		private void AddNewAddress(IVariable variable)
		{
			if(isFixed)
			{
				SetToAddress(variable, fixedAddress);
				variable.SetAddress(fixedAddress);
				return;
			}
			while(addressCounter < variables.Length && variables[addressCounter] != null)
			{
				addressCounter++;
			}
			AllocateAddress(addressCounter);
			variables[addressCounter] = variable;
			variable.SetAddress(addressCounter);
			addressCounter++;
		}

		private void AllocateAddress(uint address)
		{
			if(address >= variables.Length)
			{
				var tmp = new IVariable[address * 2];
				variables.CopyTo(tmp, 0);
				variables = tmp;
			}
		}

		public IVariable[] GetVariables(out int count)
		{
			count = (int)addressCounter;
			return variables;
		}
	}
}