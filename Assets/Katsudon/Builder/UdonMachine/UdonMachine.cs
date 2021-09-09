using System;
using System.Collections.Generic;
using System.Text;
using Katsudon.Builder.Methods;
using Katsudon.Info;
using UnityEngine;
using VRC.Udon.VM.Common;

namespace Katsudon.Builder
{
	public class UdonMachine
	{
		public const uint LAST_ALIGNED_ADDRESS = 0xFFFFFFFC;
		public static readonly IAddressLabel endProgramAddress = new EndAddressLabel();

		public AsmTypeInfo typeInfo { get; private set; }

		private ExternsCollection externsCollection;
		private ConstCollection constCollection;
		private FieldsCollection fieldsCollection;

		private List<Operation> operations = new List<Operation>();
		private uint addressCounter = 0;

		private IVariable retaddrVariable = null;
		private IVariable thisVariable = null;
		private IVariable transformVariable = null;
		private IVariable gameObjectVariable = null;

		private IList<UdonMethodMeta> metaList;

		public UdonMachine(IList<UdonMethodMeta> metaList, AsmTypeInfo typeInfo, ConstCollection constCollection,
			ExternsCollection externsCollection, FieldsCollection fieldsCollection)
		{
			this.metaList = metaList;
			this.typeInfo = typeInfo;
			this.constCollection = constCollection;
			this.externsCollection = externsCollection;
			this.fieldsCollection = fieldsCollection;
		}

		public ConstCollection GetConstCollection()
		{
			return constCollection;
		}

		public FieldsCollection GetFieldsCollection()
		{
			return fieldsCollection;
		}

		public IVariable GetConstVariable(object value)
		{
			return constCollection.GetConstVariable(value);
		}

		public IVariable GetConstVariable(object value, Type type)
		{
			return constCollection.GetConstVariable(value, type);
		}

		public IVariable GetExternIdentifierVariable(string value)
		{
			return externsCollection.GetVariable(value);
		}

		public void ApplyProperties(PropertiesBlock properties)
		{
			if(retaddrVariable != null) properties.AddVariable(retaddrVariable);
			if(thisVariable != null) properties.AddVariable(thisVariable);
			if(transformVariable != null) properties.AddVariable(transformVariable);
			if(gameObjectVariable != null) properties.AddVariable(gameObjectVariable);
		}

		public IVariable GetReturnAddressGlobal()
		{
			if(retaddrVariable == null)
			{
				retaddrVariable = new UnnamedSignificantVariable("returnAddress", typeof(uint), UdonMachine.LAST_ALIGNED_ADDRESS);
			}
			return retaddrVariable;
		}

		public IVariable GetThisVariable(UdonThisType type = UdonThisType.Behaviour)
		{
			switch(type)
			{
				case UdonThisType.Behaviour:
					if(thisVariable == null)
					{
						thisVariable = new ThisVariable(typeInfo.type);
					}
					return thisVariable;
				case UdonThisType.Transform:
					if(transformVariable == null)
					{
						transformVariable = new SelfPointingVariable("transform", typeof(Transform));
					}
					return transformVariable;
				case UdonThisType.GameObject:
					if(gameObjectVariable == null)
					{
						gameObjectVariable = new SelfPointingVariable("gameObject", typeof(GameObject));
					}
					return gameObjectVariable;
			}
			return null;
		}

		public uint GetAddressCounter()
		{
			return addressCounter;
		}

		public IVariable CreateLabelVariable()
		{
			var label = new AddressLabelVariable(GetConstVariable);
			(label as IEmbedAddressLabel).Init(GetAddressCounter);
			return label;
		}

		public void ApplyLabel(IEmbedAddressLabel label)
		{
			label.Init(GetAddressCounter);
			label.Apply();
		}

		public void AddOpcode(OpCode opCode, IAddressLabel arg = null)
		{
			operations.Add(new Operation(opCode, arg));
			addressCounter += UdonMachine.GetOpSize(opCode);
		}

		public void AddOpcode(OpCode opCode, IDeferredValue<IAddressLabel> arg)
		{
			operations.Add(new Operation(opCode, arg));
			addressCounter += UdonMachine.GetOpSize(opCode);
		}

		public void AddMethodMeta(UdonMethodMeta meta)
		{
			this.metaList.Add(meta);
		}

		public void Build()
		{
			IDeferredValue<IAddressLabel> value;
			for(var i = operations.Count - 1; i >= 0; i--)
			{
				var op = operations[i];
				switch(op.opCode)
				{
					case OpCode.PUSH:
					case OpCode.JUMP_INDIRECT:
					case OpCode.EXTERN:
					case OpCode.JUMP:
					case OpCode.JUMP_IF_FALSE:
						if((value = op.argument as IDeferredValue<IAddressLabel>) != null)
						{
							op.argument = (IAddressLabel)value.GetValue();
							operations[i] = op;
						}
						break;
				}
			}
		}

		public void Append(StringBuilder sb, IEnumerable<UBehMethodInfo> methods)
		{
			var sortedMethods = new List<UBehMethodInfo>(methods);
			sortedMethods.Sort((a, b) => a.address.CompareTo(b.address));

			uint address = 0;
			int methodIndex = 0;
			uint nextMethodAddress = sortedMethods[methodIndex].address;
			var exportedMethods = new HashSet<string>();
			foreach(var op in operations)
			{
				if(address == nextMethodAddress)
				{
					sb.AppendLine();
					var method = sortedMethods[methodIndex];
					if(method.export)
					{
						if(!exportedMethods.Add(method.name))
						{
							throw new Exception(string.Format("An exported method named '{0}' already exists", method.name));
						}

						sb.Append(".export ");
						sb.Append(method.name);
						sb.Append('\n');
						sb.Append(method.name);
						sb.Append(':');
						sb.Append('\n');
					}
					else
					{
						sb.Append("# private ");
						sb.Append(method.name);
						sb.Append(':');
						sb.Append('\n');
					}
					methodIndex++;
					if(methodIndex < sortedMethods.Count)
					{
						nextMethodAddress = sortedMethods[methodIndex].address;
					}
					else nextMethodAddress = UdonMachine.LAST_ALIGNED_ADDRESS;
				}
				//sb.AppendFormat("U_{0:X4}", address);
				sb.Append('\t');
				sb.Append(op.opCode);
				switch(op.opCode)
				{
					case OpCode.PUSH:
					case OpCode.JUMP_INDIRECT:
					case OpCode.EXTERN:
					case OpCode.JUMP:
					case OpCode.JUMP_IF_FALSE:
						sb.AppendFormat(", 0x{0:X8}", (op.argument as IAddressLabel).address);
						break;
				}
				sb.AppendLine();
				address += GetOpSize(op.opCode);
			}
		}

		private static uint GetOpSize(OpCode op)
		{
			switch(op)
			{
				case OpCode.NOP:
				case OpCode.POP:
				case OpCode.COPY:
					return 4;
				case OpCode.PUSH:
				case OpCode.JUMP_IF_FALSE:
				case OpCode.JUMP:
				case OpCode.EXTERN:
				case OpCode.ANNOTATION:
				case OpCode.JUMP_INDIRECT:
					return 8;
				default: return 0;
			}
		}

		private struct Operation
		{
			public OpCode opCode;
			public object argument;

			public Operation(OpCode opCode, object argument)
			{
				this.opCode = opCode;
				this.argument = argument;
			}
		}

		private class AddressLabelVariable : IVariable, IDeferredValue<IVariable>, IEmbedAddressLabel
		{
			public string name => throw new NotImplementedException();
			public Type type => typeof(uint);
			public uint address => throw new NotImplementedException();

			private Func<uint> pointerGetter;
			private Func<object, IVariable> constGetter;

			private IVariable variable;

			public AddressLabelVariable(Func<object, IVariable> constGetter)
			{
				this.constGetter = constGetter;
			}

			public void Use() { }

			public void Allocate(int count = 1) { }

			void IEmbedAddressLabel.Init(Func<uint> pointerGetter)
			{
				this.pointerGetter = pointerGetter;
			}

			void IEmbedAddressLabel.Apply()
			{
				variable = constGetter(pointerGetter());
				variable.Use();
			}

			IVariable IDeferredValue<IVariable>.GetValue()
			{
				return variable;
			}

			void IVariable.SetAddress(uint address)
			{
				throw new NotImplementedException();
			}
		}

		private class EndAddressLabel : IAddressLabel
		{
			public uint address => LAST_ALIGNED_ADDRESS;
		}
	}

	public enum UdonThisType
	{
		Behaviour,
		Transform,
		GameObject
	}

	public interface IAddressLabel
	{
		uint address { get; }
	}

	public interface IEmbedAddressLabel : IAddressLabel
	{
		void Init(Func<uint> pointerGetter);

		void Apply();
	}

	public class EmbedAddressLabel : IEmbedAddressLabel
	{
		uint IAddressLabel.address
		{
			get
			{
				if(!_address.HasValue) throw new Exception("Address is not assigned");
				return _address.Value;
			}
		}

		private uint? _address;
		private Func<uint> pointerGetter;

		void IEmbedAddressLabel.Init(Func<uint> pointerGetter)
		{
			this.pointerGetter = pointerGetter;
		}

		void IEmbedAddressLabel.Apply()
		{
			_address = pointerGetter();
		}
	}
}