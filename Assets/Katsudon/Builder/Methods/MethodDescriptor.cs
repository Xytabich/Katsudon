using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Katsudon.Builder
{
	public class MethodDescriptor : IMethodDescriptor
	{
		public Operation currentOp => operations[index];

		public bool isStatic { get; private set; }
		public bool stackIsEmpty => stack.Count < 1;

		public IUdonMachine machine => machineBlock.machine;

		#region info
		private string methodName;
		private IList<Operation> operations;
		private IList<IVariable> arguments;
		private IList<IVariable> locals;
		private IVariable returnVariable;
		private IAddressLabel returnAddress;

		private IMachineBlock machineBlock;
		#endregion

		private int index;
		private Stack<int> states = new Stack<int>();
		private List<IVariable> stack = new List<IVariable>();

		private Dictionary<int, uint> methodToMachineAddress = null;
		private List<MachineAddressLabel> initAddresses = new List<MachineAddressLabel>();

		public MethodDescriptor(bool isStatic, IList<IVariable> arguments, IVariable returnVariable, IAddressLabel returnAddress,
			IList<Operation> operations, IList<IVariable> locals, IMachineBlock block)
		{
			this.isStatic = isStatic;
			this.operations = operations;
			this.arguments = arguments;
			this.returnVariable = returnVariable;
			this.returnAddress = returnAddress;
			this.locals = locals;
			this.index = -1;

			this.machineBlock = block;
			methodToMachineAddress = new Dictionary<int, uint>(operations.Count);
		}

		//TODO: debug define
		public void CheckState()
		{
			if(states.Count > 0) throw new Exception("States remained on the stack, a state was not freed somewhere.");
			if(stack.Count > 0)
			{
				throw new Exception(string.Format("Values remained on the stack, a value was not used somewhere.\nStack: {1}", string.Join(", ", stack)));
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

		public void PushStack(IVariable value)
		{
			if(value == null) throw new ArgumentNullException("value");
			stack.Add(value);
		}

		public IVariable PopStack()
		{
			if(stack.Count < 1) throw new Exception("Stack is empty");
			int index = stack.Count - 1;
			IVariable value = stack[index];
			stack.RemoveAt(index);
			return value;
		}

		public IVariable PeekStack(int offset)
		{
			return stack[stack.Count - 1 - offset];
		}

		public IEnumerator<IVariable> PopMultiple(int count)
		{
			int index = stack.Count - count;
			int len = stack.Count;
			for(var i = index; i < len; i++)
			{
				yield return stack[i];
			}
			stack.RemoveRange(index, count);
		}

		ITmpVariable IMachineBlock.GetTmpVariable(Type type)
		{
			var tmp = machineBlock.GetTmpVariable(type);
			//TODO: debug define
			(tmp as ITmpVariableDebug).allocatedFrom = "IL Offset: " + currentOp.offset.ToString("X8") + "\n" + new StackTrace(true).ToString();
			return tmp;
		}

		ITmpVariable IMachineBlock.GetTmpVariable(IVariable variable)
		{
			var tmp = machineBlock.GetTmpVariable(variable);
			//TODO: debug define
			(tmp as ITmpVariableDebug).allocatedFrom = "IL Offset: " + currentOp.offset.ToString("X8") + "\n" + new StackTrace(true).ToString();
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

		private class MachineAddressLabel : IAddressLabel
		{
			public readonly int offset;
			public uint address { get; set; }

			public MachineAddressLabel(int offset)
			{
				this.offset = offset;
			}
		}
	}

	public interface IMethodDescriptor : IMethodProgram, IMethodStack, IMethodVariables, IMachineBlock
	{
		bool isStatic { get; }

		IAddressLabel GetMachineAddressLabel(int methodAddress);

		IAddressLabel GetReturnAddress();
	}

	public interface IExternBuilder
	{
		string BuildExtern(IMethodDescriptor method, IUdonMachine machine, Action<VariableMeta> pushCallback);
	}

	public interface IMethodVariables
	{
		IVariable GetReturnVariable();

		IVariable GetArgumentVariable(int index);

		IVariable GetLocalVariable(int index);
	}

	public interface IMethodStack
	{
		bool stackIsEmpty { get; }

		void PushStack(IVariable value);

		IVariable PopStack();

		IVariable PeekStack(int offset);

		IEnumerator<IVariable> PopMultiple(int count);
	}

	public interface IMethodProgram
	{
		Operation currentOp { get; }

		bool Next(bool skipNop = true);

		void PushState();

		void PopState();

		void DropState();
	}
}