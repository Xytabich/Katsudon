using System;
using System.Collections.Generic;
using VRC.Udon.VM.Common;

namespace Katsudon.Builder
{
	public class UdonMachineBlock : IMachineBlock
	{
		public IUdonMachine machine { get; private set; }

		private List<TmpVariable> tmpVariables = new List<TmpVariable>();
		private Dictionary<Type, Stack<TmpVariable>> releasedVariables = new Dictionary<Type, Stack<TmpVariable>>();

		public UdonMachineBlock(UdonMachine udonMachine, NumericConvertersList convertersList)
		{
			this.machine = new UdonBlockBuilder(udonMachine, this, convertersList);
		}

		//TODO: debug define
		public void CheckVariables()
		{
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
				throw new Exception(string.Format("Method has unreleased tmp variables ({2}):\n{3}", variables.Count, string.Join("\n", variables)));
			}
		}

		public void ApplyProperties(PropertiesBlock properties)
		{
			foreach(var variable in tmpVariables)
			{
				if(variable.isUsed) properties.AddVariable(variable);
			}
		}

		public ITmpVariable GetTmpVariable(Type type)
		{
			TmpVariable variable;
			if(releasedVariables.TryGetValue(type, out var list) && list.Count > 0)
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

		private class UdonBlockBuilder : IUdonMachine
		{
			private UdonMachine udonMachine;
			private IMachineBlock block;
			private NumericConvertersList convertersList;

			private Stack<IReferenceVariable> referencesStack = new Stack<IReferenceVariable>();

			public UdonBlockBuilder(UdonMachine udonMachine, IMachineBlock block, NumericConvertersList convertersList)
			{
				this.udonMachine = udonMachine;
				this.block = block;
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

			public uint GetAddressCounter()
			{
				return udonMachine.GetAddressCounter();
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
						reference.LoadValue(block);
						variable = reference.GetValueVariable();
					}

					var type = variableInfo.preferredType;
					if(type != variable.type && NumberCodeUtils.IsPrimitive(Type.GetTypeCode(variable.type)))
					{
						if(convertersList.TryConvert(block, variable, type, out var converted))
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
					referencesStack.Pop().StoreValue(block);
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

		private class TmpVariable : UnnamedVariable, ITmpVariable, ITmpVariableDebug
		{
			public bool isUsed = false;
			public int usesLeft = 1;
			public bool isHandle = false;

			public string allocatedFrom { get; set; }

			bool ITmpVariable.isHandle => isHandle;

			public event Action onUse;
			public event Action onRelease;

			private System.Action<TmpVariable> releaseVariable;

			public TmpVariable(string prefix, Type type, System.Action<TmpVariable> releaseVariable) : base(prefix, type)
			{
				this.releaseVariable = releaseVariable;
			}

			public override void Use()
			{
				if(isHandle) return;

				isUsed = true;
				if(onUse != null) onUse.Invoke();

				usesLeft--;
				if(usesLeft <= 0)
				{
					if(usesLeft < 0) throw new Exception("Variable has no allocated uses");
					usesLeft = 0;
					releaseVariable(this);
					if(onRelease != null) onRelease.Invoke();
				}
			}

			public override void Allocate(int count = 1)
			{
				if(isHandle) return;

				if(count < 1) return;
				if(usesLeft < 0) usesLeft = 0;
				usesLeft += count;
			}

			ITmpVariable ITmpVariable.Reserve()
			{
				isHandle = true;
				return this;
			}

			void ITmpVariable.Release()
			{
				isHandle = false;
				Use();
			}

			public override string ToString()
			{
				//TODO: debug define
				return "Tmp: " + type + "; allocated from:\n\t" + string.Join("\n\t", allocatedFrom.Split('\n'));
			}
		}
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

		uint GetAddressCounter();
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

	public interface IMachineBlock
	{
		IUdonMachine machine { get; }

		/// <summary>
		/// Creates a temporary variable with the given type
		/// </summary>
		ITmpVariable GetTmpVariable(Type type);

		/// <summary>
		/// Creates a temporary copy of a variable if the variable itself is not temporary
		/// </summary>
		ITmpVariable GetTmpVariable(IVariable variable);
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
		ITmpVariable Reserve();
		/// <summary>
		/// Releases reserved variable
		/// </summary>
		void Release();
	}

	//TODO: debug define
	internal interface ITmpVariableDebug : ITmpVariable
	{
		string allocatedFrom { get; set; }
	}
}