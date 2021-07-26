using System;
using System.Collections.Generic;
using System.Diagnostics;
using VRC.Udon.VM.Common;

namespace Katsudon.Builder
{
	public class MethodDescriptor : IMethodDescriptor
	{
		public Operation currentOp => operations[index];

		public bool isStatic { get; private set; }
		public bool stackIsEmpty => stack.Count < 1;

		public IUdonMachine machine { get; private set; }

		public Type classType { get; private set; }

		#region info
		private string methodName;
		private List<Operation> operations;
		private IVariable[] arguments;
		private IVariable returnVariable;
		private IVariable returnAddress;
		private IList<IVariable> locals;

		private Func<uint> machineAddressGetter = null;
		private Dictionary<int, uint> methodToMachineAddress = null;
		#endregion

		private int index;
		private Stack<int> states = new Stack<int>();
		private List<IVariable> stack = new List<IVariable>();

		private List<TmpVariable> tmpVariables = new List<TmpVariable>();
		private Dictionary<Type, Stack<TmpVariable>> releasedVariables = new Dictionary<Type, Stack<TmpVariable>>();
		private List<MachineAddressLabel> initAddresses = new List<MachineAddressLabel>();

		public MethodDescriptor(UBehMethodInfo uBehMethod, bool isStatic, Type type,
			List<Operation> operations, IList<IVariable> locals,
			UdonMachine udonMachine, NumericConvertersList convertersList)
		{
			this.methodName = uBehMethod.name;
			this.isStatic = isStatic;
			this.classType = type;
			this.operations = operations;
			this.arguments = uBehMethod.arguments;
			this.returnVariable = uBehMethod.ret;
			this.locals = locals;
			this.index = -1;

			this.machine = new UdonMachineBuilder(udonMachine, this, convertersList);

			returnAddress = new UnnamedSignificantVariable("returnAddress", typeof(uint), UdonMachine.LAST_ALIGNED_ADDRESS);
		}

		public void PreBuild(UdonMachine udonMachine)
		{
			this.machineAddressGetter = udonMachine.GetAddressCounter;
			methodToMachineAddress = new Dictionary<int, uint>(operations.Count);
		}

		public void PostBuild()
		{
			for(var i = 0; i < initAddresses.Count; i++)
			{
				initAddresses[i].address = methodToMachineAddress[initAddresses[i].offset];
			}
			//TODO: debug define
			if(states.Count > 0) throw new Exception("States remained on the stack, a state was not freed somewhere. Method: " + methodName);
			if(stack.Count > 0)
			{
				throw new Exception(string.Format("Values remained on the stack, a value was not used somewhere.\nMethod: {0}\nStack: {1}", methodName, string.Join(", ", stack)));
			}
			int releasedCounter = 0;
			foreach(var item in releasedVariables)
			{
				foreach(var variable in item.Value)
				{
					releasedCounter++;
				}
			}
			foreach(var variable in tmpVariables)
			{
				if(variable.isUsed) releasedCounter--;
			}
			if(releasedCounter < 0)
			{
				var variables = new HashSet<TmpVariable>();
				foreach(var variable in tmpVariables)
				{
					if(variable.isUsed) variables.Add(variable);
				}
				foreach(var item in releasedVariables)
				{
					foreach(var variable in item.Value)
					{
						variables.Remove(variable);
					}
				}
				UnityEngine.Debug.LogError(string.Format("Method '{0}' in type {1} has unreleased tmp variables ({2}):\n{3}", methodName, classType, variables.Count, string.Join("\n", variables)));
			}
		}

		public bool Next(bool skipNop = true)
		{
			do
			{
				index++;
				if(index >= operations.Count) return false;
				if(machineAddressGetter != null)
				{
					methodToMachineAddress[currentOp.offset] = machineAddressGetter();
				}
			}
			while(currentOp.opCode.Value == 0x00);
			return true;
		}

		public void ApplyProperties(PropertiesBlock properties)
		{
			properties.AddVariable(returnAddress);
			if(returnVariable != null) properties.AddVariable(returnVariable);
			foreach(var variable in arguments)
			{
				properties.AddVariable(variable);
			}
			foreach(var variable in locals)
			{
				properties.AddVariable(variable);
			}
			foreach(var variable in tmpVariables)
			{
				if(variable.isUsed) properties.AddVariable(variable);
			}
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

		public ITmpVariable GetTmpVariable(Type type)
		{
			Stack<TmpVariable> list;
			TmpVariable variable;
			if(releasedVariables.TryGetValue(type, out list) && list.Count > 0)
			{
				variable = list.Pop();
				variable.usesLeft = 1;
				variable.isHandle = false;
			}
			else
			{
				variable = new TmpVariable("tmp", type, ReleaseVariable);
				tmpVariables.Add(variable);
			}
			//TODO: debug define
			variable.allocatedFrom = "IL Offset: " + currentOp.offset.ToString("X8") + "\n" + new StackTrace(true).ToString();
			return variable;
		}

		public ITmpVariable GetTmpVariable(IVariable variable)
		{
			if(variable is ITmpVariable tmp && !tmp.isHandle)
			{
				return tmp;
			}
			else
			{
				var newTmp = GetTmpVariable(variable.type);
				newTmp.Allocate();
				machine.AddCopy(variable, newTmp);
				return newTmp;
			}
		}

		IVariable IMethodVariables.GetReturnAddressVariable()//TODO: make a jump to an exit point instead of a variable jump. the exit point code is already defined by the method builder
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

		private void ReleaseVariable(TmpVariable value)
		{
			Stack<TmpVariable> list;
			if(!releasedVariables.TryGetValue(value.type, out list))
			{
				list = new Stack<TmpVariable>();
				releasedVariables[value.type] = list;
			}
			list.Push(value);
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

		private class UdonMachineBuilder : IUdonMachine
		{
			private UdonMachine udonMachine;
			private IMethodDescriptor method;
			private NumericConvertersList convertersList;

			private Stack<IReferenceVariable> referencesStack = new Stack<IReferenceVariable>();

			public UdonMachineBuilder(UdonMachine udonMachine, IMethodDescriptor method, NumericConvertersList convertersList)
			{
				this.udonMachine = udonMachine;
				this.method = method;
				this.convertersList = convertersList;
			}

			public IVariable CreateLabelVariable()
			{
				return udonMachine.CreateLabelVariable();
			}

			public ConstCollection GetConstCollection()
			{
				return udonMachine.GetConstCollection();
			}

			public FieldsCollection GetFieldsCollection()
			{
				return udonMachine.GetFieldsCollection();
			}

			public IVariable GetConstVariable(object value)
			{
				return udonMachine.GetConstVariable(value);
			}

			public IVariable GetConstVariable(object value, Type type)
			{
				return udonMachine.GetConstVariable(value, type);
			}

			public IVariable GetReturnAddressGlobal()
			{
				return udonMachine.GetReturnAddressGlobal();
			}

			public void ApplyLabel(IEmbedAddressLabel label)
			{
				udonMachine.ApplyLabel(label);
			}

			public IVariable GetThisVariable(UdonThisType type = UdonThisType.Behaviour)
			{
				return udonMachine.GetThisVariable(type);
			}

			#region opcodes
			private void AddPush(VariableMeta variableInfo)
			{
				var variable = variableInfo.variable;
				if((variableInfo.usageMode & VariableMeta.UsageMode.Out) != 0)
				{
					if(variableInfo.variable is IReferenceVariable reference)
					{
						reference.Allocate();
						referencesStack.Push(reference);
						variable = reference.GetValueVariable();
					}
				}
				if((variableInfo.usageMode & VariableMeta.UsageMode.In) != 0)
				{
					if(variableInfo.variable is IReferenceVariable reference)
					{
						reference.LoadValue(method);
						variable = reference.GetValueVariable();
					}

					var type = variableInfo.preferredType;
					if(type != variable.type && NumberCodeUtils.IsPrimitive(Type.GetTypeCode(variable.type)))
					{
						if(convertersList.TryConvert(method, variable, type, out var converted))
						{
							variable = converted;
						}
					}
				}
				udonMachine.AddOpcode(OpCode.PUSH, variable.Used());
			}

			private void ApplyReferences()
			{
				while(referencesStack.Count > 0)
				{
					referencesStack.Pop().StoreValue(method);
				}
			}

			public void AddCopy(IVariable fromVariable, IVariable toVariable)
			{
				AddPush(fromVariable.Mode(VariableMeta.UsageMode.In));
				AddPush(toVariable.Mode(VariableMeta.UsageMode.Out));

				udonMachine.AddOpcode(OpCode.COPY);

				ApplyReferences();
			}

			/// <summary>
			/// Deferred use of the target variable, necessary for more optimal reuse of temporary variables
			/// </summary>
			public void AddCopy(IVariable fromVariable, Func<IVariable> toVariableCtor)
			{
				AddPush(fromVariable.Mode(VariableMeta.UsageMode.In));

				var toVariable = toVariableCtor();
				AddPush(toVariable.Mode(VariableMeta.UsageMode.Out));

				udonMachine.AddOpcode(OpCode.COPY);

				ApplyReferences();
			}

			public void AddCopy(IVariable fromVariable, IVariable toVariable, Type type)
			{
				AddPush(fromVariable.UseType(type).Mode(VariableMeta.UsageMode.In));
				AddPush(toVariable.Mode(VariableMeta.UsageMode.Out));

				udonMachine.AddOpcode(OpCode.COPY);

				ApplyReferences();
			}

			public void AddExtern(string name, params VariableMeta[] inVariables)
			{
				for(int i = 0; i < inVariables.Length; i++)
				{
					AddPush(inVariables[i].Mode(VariableMeta.UsageMode.In));
				}

				udonMachine.AddOpcode(OpCode.EXTERN, udonMachine.GetExternIdentifierVariable(name));

				ApplyReferences();
			}

			public void AddExtern(string name, IVariable outVariable, params VariableMeta[] inVariables)
			{
				for(int i = 0; i < inVariables.Length; i++)
				{
					AddPush(inVariables[i].Mode(VariableMeta.UsageMode.In));
				}

				outVariable.Allocate();
				AddPush(outVariable.Mode(VariableMeta.UsageMode.Out));
				udonMachine.AddOpcode(OpCode.EXTERN, udonMachine.GetExternIdentifierVariable(name));

				ApplyReferences();
			}

			public void AddExtern(string name, Func<IVariable> outVariableCtor, params VariableMeta[] inVariables)
			{
				for(int i = 0; i < inVariables.Length; i++)
				{
					AddPush(inVariables[i].Mode(VariableMeta.UsageMode.In));
				}

				var outVariable = outVariableCtor();
				outVariable.Allocate();
				AddPush(outVariable.Mode(VariableMeta.UsageMode.Out));

				udonMachine.AddOpcode(OpCode.EXTERN, udonMachine.GetExternIdentifierVariable(name));

				ApplyReferences();
			}

			public void AddJump(IAddressLabel label)
			{
				udonMachine.AddOpcode(OpCode.JUMP, label);
			}

			public void AddJump(IVariable variable)
			{
				if(variable.type != typeof(uint)) throw new InvalidOperationException("The variable must be an unsigned 32-bit integer");
				udonMachine.AddOpcode(OpCode.JUMP_INDIRECT, variable);
			}

			public void AddBranch(IVariable condition, IAddressLabel labelIfFalse)
			{
				AddPush(condition.UseType(typeof(bool)).Mode(VariableMeta.UsageMode.In));

				udonMachine.AddOpcode(OpCode.JUMP_IF_FALSE, labelIfFalse);
			}
			#endregion
		}

		private class TmpVariable : UnnamedVariable, ITmpVariable
		{
			public bool isUsed = false;
			public int usesLeft = 1;
			public bool isHandle = false;

			public string allocatedFrom;

			bool ITmpVariable.isHandle => isHandle;

			public event Action onUse;
			public event Action onRelease;

			private System.Action<TmpVariable> releaseVariable;

			public TmpVariable(string prefix, Type type, System.Action<TmpVariable> releaseVariable) : base(prefix, type)
			{
				this.releaseVariable = releaseVariable;
			}

			public void Reserve()
			{
				isHandle = true;
			}

			public void Release()
			{
				usesLeft = 0;
				releaseVariable(this);
				if(onRelease != null) onRelease.Invoke();
			}

			public override void Use()
			{
				isUsed = true;
				if(onUse != null) onUse.Invoke();
				if(isHandle) return;

				usesLeft--;
				if(usesLeft <= 0) Release();
			}

			public override void Allocate(int count = 1)
			{
				if(count < 1) return;
				if(usesLeft < 0) usesLeft = 0;
				usesLeft += count;
			}

			public override string ToString()
			{
				//TODO: debug define
				return "Tmp: " + type + "; allocated from:\n\t" + string.Join("\n\t", allocatedFrom.Split('\n'));
			}
		}
	}

	public interface IMethodDescriptor : IMethodProgram, IMethodStack, IMethodVariables
	{
		bool isStatic { get; }

		Type classType { get; }

		IUdonMachine machine { get; }

		/// <summary>
		/// Creates a temporary variable with the given type
		/// </summary>
		ITmpVariable GetTmpVariable(Type type);

		/// <summary>
		/// Creates a temporary copy of a variable if the variable itself is not temporary
		/// </summary>
		ITmpVariable GetTmpVariable(IVariable variable);

		IAddressLabel GetMachineAddressLabel(int methodAddress);
	}

	public interface IUdonMachine : IUdonProgramBuilder
	{
		ConstCollection GetConstCollection();

		FieldsCollection GetFieldsCollection();

		IVariable GetReturnAddressGlobal();

		IVariable GetThisVariable(UdonThisType type = UdonThisType.Behaviour);

		IVariable GetConstVariable(object value);

		IVariable GetConstVariable(object value, Type type);

		IVariable CreateLabelVariable();

		void ApplyLabel(IEmbedAddressLabel label);
	}

	public interface IUdonProgramBuilder
	{
		void AddCopy(IVariable fromVariable, IVariable toVariable);

		void AddCopy(IVariable fromVariable, Func<IVariable> toVariableCtor);

		void AddCopy(IVariable fromVariable, IVariable toVariable, Type type);

		void AddExtern(string name, params VariableMeta[] pushVariables);

		/// <param name="outVariable">Allocate is automatically called</param>
		void AddExtern(string name, IVariable outVariable, params VariableMeta[] inVariables);

		/// <param name="outVariableCtor">Allocate is automatically called</param>
		void AddExtern(string name, Func<IVariable> outVariableCtor, params VariableMeta[] pushVariables);

		/// <summary>
		/// Jump to address
		/// </summary>
		void AddJump(IAddressLabel label);

		/// <summary>
		/// Jump to the address contained in the variable
		/// </summary>
		void AddJump(IVariable variable);

		void AddBranch(IVariable condition, IAddressLabel labelIfFalse);
	}

	public interface IExternBuilder
	{
		string BuildExtern(IMethodDescriptor method, IUdonMachine machine, Action<VariableMeta> pushCallback);
	}

	public interface IMethodVariables
	{
		IVariable GetReturnAddressVariable();

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

	public interface ITmpVariable : IVariable
	{
		bool isHandle { get; }

		event Action onUse;
		event Action onRelease;

		/// <summary>
		/// Marks this variable as reserved, i.e. it will not be released until Release is called.
		/// Use this if a value will be assigned to this variable more than once.
		/// </summary>
		void Reserve();
		/// <summary>
		/// Releases reserved variable
		/// </summary>
		void Release();
	}

	public struct VariableMeta
	{
		public readonly IVariable variable;
		public readonly Type preferredType;
		public readonly UsageMode usageMode;

		public VariableMeta(IVariable variable, Type preferredType)
		{
			this.variable = variable;
			this.preferredType = preferredType;
			this.usageMode = UsageMode.None;
		}

		public VariableMeta(IVariable variable, Type preferredType, UsageMode usageMode)
		{
			this.variable = variable;
			this.preferredType = preferredType;
			this.usageMode = usageMode;
		}

		public enum UsageMode
		{
			None = 0,
			In = 1,
			Out = 2
		}
	}

	public static class VariableMetaExtension
	{
		public static VariableMeta UseType(this IVariable variable, Type preferredType)
		{
			return new VariableMeta(variable, preferredType.IsByRef ? preferredType.GetElementType() : preferredType);
		}

		public static VariableMeta OwnType(this IVariable variable)
		{
			return UseType(variable, variable.type);
		}

		public static VariableMeta Mode(this IVariable variable, VariableMeta.UsageMode mode)
		{
			return Mode(OwnType(variable), mode);
		}

		public static VariableMeta Mode(this VariableMeta variable, VariableMeta.UsageMode mode)
		{
			return new VariableMeta(variable.variable, variable.preferredType, variable.usageMode | mode);
		}
	}
}