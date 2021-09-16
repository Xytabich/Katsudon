using System;
using System.Collections.Generic;
using System.Diagnostics;
using Katsudon.Info;

namespace Katsudon.Builder.Methods
{
	public class MethodDescriptor : IMethodDescriptor
	{
		public Operation currentOp => operations[index];

		public bool isStatic { get; private set; }
		public bool stackIsEmpty => stack.Count < 1;

		public IUdonMachine machine { get; private set; }

		#region info
		private string methodName;
		private IList<Operation> operations;
		private IList<IVariable> arguments;
		private IList<IVariable> locals;
		private IVariable returnVariable;
		private IAddressLabel returnAddress;

		private IUdonProgramBlock machineBlock;
		#endregion

		private int index;
		private Stack<int> states = new Stack<int>();//TODO: cache
		private List<IVariable> stack = new List<IVariable>();
		private Stack<int> volatileStack = new Stack<int>();

		private Dictionary<int, uint> methodToMachineAddress = null;
		private List<MachineAddressLabel> initAddresses = new List<MachineAddressLabel>();

		private uint lastUdonAddress;
		private IList<UdonAddressPointer> addressPointers;

		public MethodDescriptor(bool isStatic, IList<IVariable> arguments, IVariable returnVariable, IAddressLabel returnAddress,
			IList<Operation> operations, IList<IVariable> locals, IUdonProgramBlock block, IList<UdonAddressPointer> addressPointers)
		{
			this.isStatic = isStatic;
			this.operations = operations;
			this.arguments = arguments;
			this.returnVariable = returnVariable;
			this.returnAddress = returnAddress;
			this.locals = locals;
			this.index = -1;

			this.lastUdonAddress = block.machine.GetAddressCounter();
			this.addressPointers = addressPointers;

			this.machineBlock = block;
			this.machine = new MethodOpTracker((IRawUdonMachine)machineBlock.machine, this);
			methodToMachineAddress = new Dictionary<int, uint>(operations.Count);//TODO: cache
		}

		//TODO: debug define
		public void CheckState()
		{
			if(states.Count > 0) throw new Exception("States remained on the stack, a state was not freed somewhere.");
			if(stack.Count > 0)
			{
				throw new Exception(string.Format("Values remained on the stack, a value was not used somewhere.\nStack: {0}", string.Join(", ", stack)));
			}
		}

		public void ApplyProperties()
		{
			for(var i = 0; i < initAddresses.Count; i++)
			{
				initAddresses[i].address = methodToMachineAddress[initAddresses[i].offset];
			}
		}

		public bool Next(bool skipNop = true)
		{
			do
			{
				index++;
				if(index >= operations.Count) return false;
				if(machineBlock.machine.GetAddressCounter() != lastUdonAddress)
				{
					lastUdonAddress = machineBlock.machine.GetAddressCounter();
					addressPointers.Add(new UdonAddressPointer(lastUdonAddress, currentOp.offset));
				}
				methodToMachineAddress[currentOp.offset] = machineBlock.machine.GetAddressCounter();
			}
			while(currentOp.opCode.Value == 0x00);
			return true;
		}

		public void PushState()
		{
			states.Push(index);
		}

		public void PopState()
		{
			index = states.Pop();
		}

		public void DropState()
		{
			states.Pop();
		}

		public void PushStack(IVariable value, bool isVolatile = false)
		{
			if(value == null) throw new ArgumentNullException("value");
			if(isVolatile) volatileStack.Push(stack.Count);
			stack.Add(value);
		}

		public IVariable PopStack()
		{
			if(stack.Count < 1) throw new Exception("Stack is empty");
			int index = stack.Count - 1;
			IVariable value = stack[index];
			stack.RemoveAt(index);
			OnStackPop(stack.Count);
			return value;
		}

		public IVariable PeekStack(int offset)
		{
			return stack[stack.Count - 1 - offset];
		}

		public IEnumerator<IVariable> PopMultiple(int count)
		{
			int index = stack.Count - count;
			int endIndex = stack.Count - 1;
			for(int i = endIndex; i >= index; i--)
			{
				OnStackPop(i);
			}
			for(var i = index; i <= endIndex; i++)
			{
				yield return stack[i];
			}
			stack.RemoveRange(index, count);
		}

		public ITmpVariable GetTmpVariable(Type type)
		{
			var tmp = machineBlock.GetTmpVariable(type);
			//TODO: debug define
			(tmp as ITmpVariableDebug).allocatedFrom = "IL Offset: " + currentOp.offset.ToString("X8") + "\n" + new StackTrace(true).ToString();
			return tmp;
		}

		public ITmpVariable GetTmpVariable(VariableMeta variable)
		{
			var tmp = machineBlock.GetTmpVariable(variable);
			//TODO: debug define
			(tmp as ITmpVariableDebug).allocatedFrom = "IL Offset: " + currentOp.offset.ToString("X8") + "\n" + new StackTrace(true).ToString();
			return tmp;
		}

		IVariable IUdonProgramBlock.GetReadonlyVariable(VariableMeta variable)
		{
			var tmp = machineBlock.GetReadonlyVariable(variable);
			//TODO: debug define
			if(tmp is ITmpVariableDebug debug)
			{
				debug.allocatedFrom = "IL Offset: " + currentOp.offset.ToString("X8") + "\n" + new StackTrace(true).ToString();
			}
			return tmp;
		}

		IAddressLabel IMethodDescriptor.GetReturnAddress()
		{
			return returnAddress;
		}

		IVariable IMethodVariables.GetReturnVariable()
		{
			return returnVariable;
		}

		IVariable IMethodVariables.GetArgumentVariable(int index)
		{
			return arguments[index];
		}

		IVariable IMethodVariables.GetLocalVariable(int index)
		{
			return locals[index];
		}

		IAddressLabel IMethodDescriptor.GetMachineAddressLabel(int methodAddress)
		{
			var label = new MachineAddressLabel(methodAddress);
			initAddresses.Add(label);
			return label;
		}

		private void OnStackPop(int index)
		{
			if(volatileStack.Count != 0 && volatileStack.Peek() == index)
			{
				volatileStack.Pop();
			}
		}

		private void OnUnreliableAction()
		{
			if(volatileStack.Count == 0) return;
			var created = new Dictionary<IVariable, IVariable>(volatileStack.Count);//TODO: cache
			foreach(var index in volatileStack)
			{
				var variable = stack[index];
				if(created.TryGetValue(variable, out var tmp))
				{
					tmp.Allocate();
				}
				else
				{
					tmp = GetTmpVariable(variable.OwnType());
					created[variable] = tmp;
				}
				stack[index] = tmp;
			}
		}

		private class MachineAddressLabel : IAddressLabel
		{
			public readonly int offset;
			public uint address { get; set; }

			public MachineAddressLabel(int offset)
			{
				this.offset = offset;
			}
		}

		private class MethodOpTracker : IRawUdonMachine
		{
			public AsmTypeInfo typeInfo => machine.typeInfo;

			public UdonMachine mainMachine => machine.mainMachine;

			private IRawUdonMachine machine;
			private MethodDescriptor method;

			public MethodOpTracker(IRawUdonMachine machine, MethodDescriptor method)
			{
				this.machine = machine;
				this.method = method;
			}

			public void AddCopy(IVariable fromVariable, IVariable toVariable)
			{
				machine.AddCopy(fromVariable, toVariable);
			}

			public void AddCopy(IVariable fromVariable, Func<IVariable> toVariableCtor)
			{
				machine.AddCopy(fromVariable, toVariableCtor);
			}

			public void AddCopy(IVariable fromVariable, IVariable toVariable, Type type)
			{
				machine.AddCopy(fromVariable, toVariable, type);
			}

			public void AddExtern(string name, params VariableMeta[] pushVariables)
			{
				method.OnUnreliableAction();
				machine.AddExtern(name, pushVariables);
			}

			public void AddExtern(string name, IVariable outVariable, params VariableMeta[] inVariables)
			{
				method.OnUnreliableAction();
				machine.AddExtern(name, outVariable, inVariables);
			}

			public void AddExtern(string name, Func<IVariable> outVariableCtor, params VariableMeta[] pushVariables)
			{
				method.OnUnreliableAction();
				machine.AddExtern(name, outVariableCtor, pushVariables);
			}

			public void AddJump(IAddressLabel label)
			{
				method.OnUnreliableAction();
				machine.AddJump(label);
			}

			public void AddJump(IVariable variable)
			{
				method.OnUnreliableAction();
				machine.AddJump(variable);
			}

			public void AddBranch(IVariable condition, IAddressLabel labelIfFalse)
			{
				method.OnUnreliableAction();
				machine.AddBranch(condition, labelIfFalse);
			}

			public IVariable GetConstVariable(object value)
			{
				return machine.GetConstVariable(value);
			}

			public IVariable GetConstVariable(object value, Type type)
			{
				return machine.GetConstVariable(value, type);
			}

			public IVariable GetThisVariable(UdonThisType type = UdonThisType.Behaviour)
			{
				return machine.GetThisVariable(type);
			}

			public IVariable GetReturnAddressGlobal()
			{
				return machine.GetReturnAddressGlobal();
			}

			public IVariable CreateLabelVariable()
			{
				return machine.CreateLabelVariable();
			}

			public void ApplyLabel(IEmbedAddressLabel label)
			{
				machine.ApplyLabel(label);
			}

			public uint GetAddressCounter()
			{
				return machine.GetAddressCounter();
			}

			public void AddMethodMeta(UdonMethodMeta methodMeta)
			{
				machine.AddMethodMeta(methodMeta);
			}

			public void AddPush(VariableMeta variableInfo)
			{
				machine.AddPush(variableInfo);
			}

			public void ApplyReferences()
			{
				machine.ApplyReferences();
			}
		}
	}

	public struct UdonAddressPointer
	{
		public uint udonAddress;
		public int ilOffset;

		public UdonAddressPointer(uint udonAddress, int ilOffset)
		{
			this.udonAddress = udonAddress;
			this.ilOffset = ilOffset;
		}
	}

	public struct UdonMethodMeta : IComparable<UdonMethodMeta>
	{
		public string assemblyName;
		public string moduleName;
		public int methodToken;
		public uint startAddress;
		public uint endAddress;
		public IReadOnlyList<UdonAddressPointer> pointers;

		public UdonMethodMeta(string assemblyName, string moduleName, int methodToken, uint startAddress, uint endAddress, IReadOnlyList<UdonAddressPointer> pointers)
		{
			this.assemblyName = assemblyName;
			this.moduleName = moduleName;
			this.methodToken = methodToken;
			this.startAddress = startAddress;
			this.endAddress = endAddress;
			this.pointers = pointers;
		}

		public int CompareTo(UdonMethodMeta other)
		{
			if(startAddress == other.startAddress)
			{
				int c = methodToken.CompareTo(other.methodToken);
				if(c != 0) return c;
				c = moduleName.CompareTo(other.moduleName);
				if(c != 0) return c;
				return assemblyName.CompareTo(other.assemblyName);
			}
			else
			{
				return startAddress.CompareTo(other.startAddress);
			}
		}
	}
}