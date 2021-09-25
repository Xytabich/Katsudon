using System;
using System.Collections.Generic;
using System.Diagnostics;
using Katsudon.Info;
using VRC.Udon.VM.Common;

namespace Katsudon.Builder.Methods
{
	public class MethodDescriptor : IMethodDescriptor, IMethodProgramRW, IDisposable
	{
		public Operation currentOp => operations[_index];
		public bool isLastOp => _index >= (operations.Count - 1);

		public bool isStatic { get; private set; }
		public bool stackIsEmpty => stack.Count < 1;

		public IUdonMachine machine { get; private set; }

		int IMethodProgramRW.index { get { return _index; } set { _index = value; } }
		int IMethodProgramRW.operationsCount => operations.Count;

		#region info
		private string methodName;
		private IList<Operation> operations;
		private IList<IVariable> arguments;
		private IList<IVariable> locals;
		private IVariable returnVariable;
		private IAddressLabel returnAddress;

		private IUdonProgramBlock machineBlock;
		#endregion

		private int _index;
		private List<IVariable> stack;
		private Stack<int> volatileStack;
		private SortedList<int, StoredStack> storedStacks;

		private Dictionary<int, uint> methodToMachineAddress;
		private List<MachineAddressLabel> initAddresses;

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
			this._index = -1;

			this.lastUdonAddress = block.machine.GetAddressCounter();
			this.addressPointers = addressPointers;

			this.machineBlock = block;
			this.machine = new MethodOpTracker((IRawUdonMachine)machineBlock.machine, this);

			stack = CollectionCache.GetList<IVariable>();
			volatileStack = CollectionCache.GetStack<int>();
			storedStacks = CollectionCache.GetSortedList<int, StoredStack>();
			methodToMachineAddress = CollectionCache.GetDictionary<int, uint>();
			initAddresses = CollectionCache.GetList<MachineAddressLabel>();
		}

#if KATSUDON_DEBUG
		public void CheckState()
		{
			if(stack.Count > 0)
			{
				throw new Exception(string.Format("Values remained on the stack, a value was not used somewhere.\nStack: {0}", string.Join(", ", stack)));
			}
		}
#endif

		public void ApplyProperties()
		{
			for(var i = 0; i < initAddresses.Count; i++)
			{
				initAddresses[i].address = methodToMachineAddress[initAddresses[i].offset];
			}
		}

		public void Dispose()
		{
			CollectionCache.Release(stack);
			CollectionCache.Release(volatileStack);
			CollectionCache.Release(storedStacks);
			CollectionCache.Release(methodToMachineAddress);
			CollectionCache.Release(initAddresses);
		}

		public bool Next()
		{
			do
			{
				_index++;
				if(_index >= operations.Count) return false;
				if(machineBlock.machine.GetAddressCounter() != lastUdonAddress)
				{
					lastUdonAddress = machineBlock.machine.GetAddressCounter();
					addressPointers.Add(new UdonAddressPointer(lastUdonAddress, currentOp.offset));
				}
				if(storedStacks.Count > 0 && storedStacks.Keys[0] == currentOp.offset)
				{
					var stored = storedStacks.Values[0];
					storedStacks.RemoveAt(0);
					var stackHandle = ApplyStoredStack(stored);
					methodToMachineAddress[currentOp.offset] = machineBlock.machine.GetAddressCounter();
					stackHandle.Dispose();
				}
				else
				{
					methodToMachineAddress[currentOp.offset] = machineBlock.machine.GetAddressCounter();
				}
			}
			while(currentOp.opCode.Value == 0x00);
			return true;
		}

		public StateHandle GetStateHandle()
		{
			return new StateHandle(this);
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

		public IDisposable StoreBranchingStack(int loadIlOffset, bool clearStack)
		{
			var rawMachine = (IRawUdonMachine)machine;
			foreach(var variable in stack)
			{
				variable.Allocate();
				rawMachine.AddPush(variable.OwnType().Mode(VariableMeta.UsageMode.In));
			}
			storedStacks.Add(loadIlOffset, new StoredStack(stack, volatileStack));
			if(clearStack)
			{
				stack.Clear();
				volatileStack.Clear();
			}
			return new PopHandle(rawMachine.mainMachine, stack.Count);
		}

		public ITmpVariable GetTmpVariable(Type type)
		{
			var tmp = machineBlock.GetTmpVariable(type);
#if KATSUDON_DEBUG
			(tmp as ITmpVariableDebug).allocatedFrom = "IL Offset: " + currentOp.offset.ToString("X8") + "\n" + new StackTrace(true).ToString();
#endif
			return tmp;
		}

		public ITmpVariable GetTmpVariable(VariableMeta variable)
		{
			var tmp = machineBlock.GetTmpVariable(variable);
#if KATSUDON_DEBUG
			(tmp as ITmpVariableDebug).allocatedFrom = "IL Offset: " + currentOp.offset.ToString("X8") + "\n" + new StackTrace(true).ToString();
#endif
			return tmp;
		}

		IVariable IUdonProgramBlock.GetReadonlyVariable(VariableMeta variable)
		{
			var tmp = machineBlock.GetReadonlyVariable(variable);
#if KATSUDON_DEBUG
			if(tmp is ITmpVariableDebug debug)
			{
				debug.allocatedFrom = "IL Offset: " + currentOp.offset.ToString("X8") + "\n" + new StackTrace(true).ToString();
			}
#endif
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

		private CopyStoredHandle ApplyStoredStack(StoredStack stored)
		{
			var rawMachine = (IRawUdonMachine)machine;
			if(stack.Count > 0)
			{
				if(stack.Count != stored.stack.Count) throw new Exception("Stacks must match");
				var storedStack = stored.stack;
				int index = 0;
				var iterator = PopMultiple(stack.Count);
				while(iterator.MoveNext())
				{
					storedStack[index].Use();
					rawMachine.AddPush(iterator.Current.UseType(storedStack[index].type).Mode(VariableMeta.UsageMode.In));
				}
				return new CopyStoredHandle(stored, this, rawMachine.mainMachine);
			}
			else
			{
				// FIX: It would be nice to do some preliminary analysis after all, or make some dynamic address instead of GetAddressCounter, and delete unnecessary code on the fly.
				new PopHandle(rawMachine.mainMachine, stored.stack.Count).Dispose();
				foreach(var variable in stored.stack)
				{
					PushStack(variable);
				}
				foreach(var index in stored.volatileStack)
				{
					volatileStack.Push(index);
				}
				stored.Dispose();
				return default;
			}
		}

		private void OnUnreliableAction()
		{
			if(volatileStack.Count == 0) return;
			var created = CollectionCache.GetDictionary<IVariable, IVariable>();
			foreach(var index in volatileStack)
			{
				var variable = stack[index];
				if(created.TryGetValue(variable, out var tmp))
				{
					variable.Use();
					tmp.Allocate();
				}
				else
				{
					tmp = GetTmpVariable(variable.OwnType());
					created[variable] = tmp;
				}
				stack[index] = tmp;
			}
			volatileStack.Clear();
			CollectionCache.Release(created);
		}

		private struct PopHandle : IDisposable
		{
			private UdonMachine machine;
			private int popCount;

			public PopHandle(UdonMachine machine, int popCount)
			{
				this.machine = machine;
				this.popCount = popCount;
			}

			public void Dispose()
			{
				for(int i = 0; i < popCount; i++)
				{
					machine.AddOpcode(OpCode.POP);
				}
			}
		}

		private struct CopyStoredHandle : IDisposable
		{
			private StoredStack stored;
			private IMethodDescriptor method;
			private UdonMachine udonMachine;

			public CopyStoredHandle(StoredStack stored, IMethodDescriptor method, UdonMachine udonMachine)
			{
				this.stored = stored;
				this.method = method;
				this.udonMachine = udonMachine;
			}

			public void Dispose()
			{
				if(method == null) return;
				var stack = stored.stack;
				for(int i = stack.Count - 1; i >= 0; i--)
				{
					var variable = method.GetTmpVariable(stack[i].type);
					udonMachine.AddOpcode(OpCode.PUSH, variable);
					udonMachine.AddOpcode(OpCode.COPY);
					stack[i] = variable;
				}
				for(int i = 0; i < stack.Count; i++)
				{
					method.PushStack(stack[i]);
				}
				stored.Dispose();
			}
		}

		private struct StoredStack : IDisposable
		{
			public List<IVariable> stack;
			public List<int> volatileStack;

			public StoredStack(IReadOnlyCollection<IVariable> stack, IReadOnlyCollection<int> volatileStack)
			{
				this.stack = CollectionCache.GetList(stack);
				this.volatileStack = CollectionCache.GetList(volatileStack);
			}

			public void Dispose()
			{
				CollectionCache.Release(stack);
				CollectionCache.Release(volatileStack);
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

	public interface IMethodProgram
	{
		Operation currentOp { get; }

		bool isLastOp { get; }

		bool Next();

		StateHandle GetStateHandle();
	}

	internal interface IMethodProgramRW : IMethodProgram
	{
		int index { get; set; }

		int operationsCount { get; }
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

	public struct StateHandle
	{
		private IMethodProgramRW method;
		private int index;

		internal StateHandle(IMethodProgramRW method)
		{
			this.method = method;
			this.index = method.index;
		}

		public bool Next()
		{
			do
			{
				method.index++;
				if(method.index >= method.operationsCount) return false;
			}
			while(method.currentOp.opCode.Value == 0x00);
			return true;
		}

		public void Apply()
		{
			int toIndex = method.index;
			method.index = index;
			while(method.index < toIndex)
			{
				method.Next();
			}
		}

		public void Drop()
		{
			method.index = index;
		}
	}
}