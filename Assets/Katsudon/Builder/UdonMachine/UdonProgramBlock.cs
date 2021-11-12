using System;
using System.Collections.Generic;
using System.Diagnostics;
using Katsudon.Builder.Methods;
using Katsudon.Info;
using VRC.Udon.VM.Common;

namespace Katsudon.Builder
{
	public class UdonProgramBlock : IUdonProgramBlock
	{
		public IUdonMachine machine { get; private set; }

		private Dictionary<Type, Stack<TmpVariable>> releasedVariables = new Dictionary<Type, Stack<TmpVariable>>();
#if KATSUDON_DEBUG
		private HashSet<TmpVariable> variablesInUse = new HashSet<TmpVariable>();
#endif

		public UdonProgramBlock(UdonMachine udonMachine, PrimitiveConvertersList convertersList)
		{
			this.machine = new UdonBlockBuilder(udonMachine, this, convertersList);
		}

#if KATSUDON_DEBUG
		public void CheckVariables()
		{
			if(variablesInUse.Count > 0)
			{
				variablesInUse.RemoveWhere(UnusedVariablesFilter);
				if(variablesInUse.Count > 0)
				{
					throw new Exception(string.Format("Block has unreleased tmp variables ({0}):\n{1}", variablesInUse.Count, string.Join("\n", variablesInUse)));
				}
			}
		}
#endif

		public void ApplyProperties(PropertiesBlock properties)
		{
			foreach(var pair in releasedVariables)
			{
				foreach(var variable in pair.Value)
				{
					properties.AddVariable(variable);
				}
			}
		}

		public ITmpVariable GetTmpVariable(Type type)
		{
			TmpVariable variable;
			if(releasedVariables.TryGetValue(type, out var list) && list.Count > 0)
			{
				variable = list.Pop();
				variable.usesLeft = 1;
				variable.handleAllocations = 0;
				variable.isReadOnly = false;
			}
			else
			{
				variable = new TmpVariable("tmp", type, this);
			}
#if KATSUDON_DEBUG
			if(!variablesInUse.Add(variable)) throw new Exception("This variable is already in use");
			if(variable is ITmpVariableDebug debug)
			{
				debug.allocatedFrom = new StackTrace(true).ToString();
			}
#endif
			return variable;
		}

		public ITmpVariable GetTmpVariable(VariableMeta variable)
		{
			if(variable.variable is TmpVariable tmp && tmp.handleAllocations == 0 && (tmp.usesLeft == 1 || !tmp.isReadOnly))
			{
				tmp.isReadOnly = false;
				return tmp;
			}
			else
			{
				var newTmp = GetTmpVariable(variable.preferredType);
				newTmp.Allocate();
				machine.AddCopy(variable.variable, newTmp, variable.preferredType);
				return newTmp;
			}
		}

		public IVariable GetReadonlyVariable(VariableMeta variable)
		{
			if(variable.variable is TmpVariable tmp && (tmp.isReadOnly || tmp.usesLeft == 1 && tmp.handleAllocations == 0))
			{
				tmp.isReadOnly = true;
				return tmp;
			}
			if(variable.variable is IFixedVariableValue)
			{
				return variable.variable;
			}

			var newTmp = (TmpVariable)GetTmpVariable(variable.preferredType);
			newTmp.Allocate();
			machine.AddCopy(variable.variable, newTmp, variable.preferredType);
			newTmp.isReadOnly = true;
			return newTmp;
		}

		private void ReleaseVariable(TmpVariable variable)
		{
#if KATSUDON_DEBUG
			if(!variablesInUse.Remove(variable)) throw new Exception("This variable is not used:\n\t" + variable.ToString().Replace("\n", "\n\t"));
#endif

			Stack<TmpVariable> list;
			if(!releasedVariables.TryGetValue(variable.type, out list))
			{
				list = new Stack<TmpVariable>();
				releasedVariables[variable.type] = list;
			}
			list.Push(variable);
		}

		private static bool UnusedVariablesFilter(TmpVariable variable)
		{
			return !variable.isUsed;
		}

		private class UdonBlockBuilder : IUdonMachine, IRawUdonMachine
		{
			public AsmTypeInfo typeInfo => udonMachine.typeInfo;

			public UdonMachine mainMachine => udonMachine;

			private UdonMachine udonMachine;
			private IUdonProgramBlock block;
			private PrimitiveConvertersList convertersList;

			private Stack<int> referencesStackStart = new Stack<int>();
			private Stack<IReferenceVariable> referencesStack = new Stack<IReferenceVariable>();

			public UdonBlockBuilder(UdonMachine udonMachine, IUdonProgramBlock block, PrimitiveConvertersList convertersList)
			{
				this.udonMachine = udonMachine;
				this.block = block;
				this.convertersList = convertersList;
			}

			public IVariable CreateLabelVariable()
			{
				return udonMachine.CreateLabelVariable();
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

			public IVariable GetThisVariable(UdonThisType type = UdonThisType.Self)
			{
				return udonMachine.GetThisVariable(type);
			}

			public uint GetAddressCounter()
			{
				return udonMachine.GetAddressCounter();
			}

			public void AddMethodMeta(UdonMethodMeta methodMeta)
			{
				udonMachine.AddMethodMeta(methodMeta);
			}

			#region opcodes
			public void AddPush(VariableMeta variableInfo)
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
				if((variableInfo.usageMode & (VariableMeta.UsageMode.In | VariableMeta.UsageMode.OutOnly)) == VariableMeta.UsageMode.In)
				{
					if(variableInfo.variable is IReferenceVariable reference)
					{
						referencesStackStart.Push(referencesStack.Count);
						reference.LoadValue(block);
						referencesStackStart.Pop();
						variable = reference.GetValueVariable();
					}

					var type = variableInfo.preferredType;
					if(type != variable.type && convertersList.TryConvert(block, variable, type, out var converted))
					{
						variable = converted;
					}
				}
				udonMachine.AddOpcode(OpCode.PUSH, variable.Used());
			}

			public void ApplyReferences()
			{
				int stopCount = referencesStackStart.Count > 0 ? referencesStackStart.Peek() : 0;
				while(referencesStack.Count != stopCount)
				{
					var reference = referencesStack.Pop();
					referencesStackStart.Push(referencesStack.Count);
					reference.StoreValue(block);
					reference.Use();
					referencesStackStart.Pop();
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
				if(!variable.type.IsAssignableFrom(typeof(uint))) throw new InvalidOperationException("The variable must be an unsigned 32-bit integer");
				variable.Use();
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
			public bool isReadOnly = false;

			public int handleAllocations = 0;

#if KATSUDON_DEBUG
			public string allocatedFrom { get; set; }
#endif

			public event Action onUse;
			public event Action onRelease;

			private UdonProgramBlock block;

			public TmpVariable(string prefix, Type type, UdonProgramBlock block) : base(prefix, type)
			{
				this.block = block;
			}

			public override void Use()
			{
				if(handleAllocations != 0) return;

				isUsed = true;
				if(onUse != null) onUse.Invoke();

				usesLeft--;
				if(usesLeft < 0) throw new Exception("Variable has no allocated uses\n" + this);
				if(usesLeft == 0)
				{
					block.ReleaseVariable(this);
					if(onRelease != null) onRelease.Invoke();
				}
			}

			public override void Allocate(int count = 1)
			{
				if(handleAllocations != 0) return;

				if(count < 1) return;
				if(usesLeft < 0) usesLeft = 0;
				usesLeft += count;
			}

			ITmpVariable ITmpVariable.Reserve()
			{
				handleAllocations++;
				return this;
			}

			void ITmpVariable.Release()
			{
				handleAllocations--;
				Use();
			}

			public override string ToString()
			{
#if KATSUDON_DEBUG
				return "Tmp: " + type + "; allocated from:\n\t" + string.Join("\n\t", allocatedFrom.Split('\n'));
#else
				return "Tmp: " + type;
#endif
			}
		}
	}

	public interface IRawUdonMachine : IUdonMachine
	{
		UdonMachine mainMachine { get; }

		void AddPush(VariableMeta variableInfo);

		void ApplyReferences();
	}

	public interface IUdonMachine : IUdonProgramBuilder
	{
		AsmTypeInfo typeInfo { get; }

		IVariable GetReturnAddressGlobal();

		IVariable GetThisVariable(UdonThisType type = UdonThisType.Self);

		IVariable GetConstVariable(object value);

		IVariable GetConstVariable(object value, Type type);

		IVariable CreateLabelVariable();

		void ApplyLabel(IEmbedAddressLabel label);

		uint GetAddressCounter();

		void AddMethodMeta(UdonMethodMeta methodMeta);
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

	public interface IUdonProgramBlock
	{
		IUdonMachine machine { get; }

		/// <summary>
		/// Creates a temporary variable with the given type
		/// </summary>
		ITmpVariable GetTmpVariable(Type type);

		/// <summary>
		/// Creates a temporary copy of a variable if the variable itself is not temporary
		/// </summary>
		ITmpVariable GetTmpVariable(VariableMeta variable);

		/// <summary>
		/// Creates a temporary copy of the variable if it is volatile. Otherwise, it returns the same variable.
		/// </summary>
		IVariable GetReadonlyVariable(VariableMeta variable);
	}

	public interface ITmpVariable : IVariable
	{
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

	internal interface ITmpVariableDebug : ITmpVariable
	{
#if KATSUDON_DEBUG
		string allocatedFrom { get; set; }
#endif
	}
}