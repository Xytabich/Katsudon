using System;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;
using Katsudon.Info;
using UnityEngine;
using VRC.Udon;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class CastOpcode : IOperationBuider
	{
		public int order => 0;

		private AssembliesInfo assemblies;

		public CastOpcode(AssembliesInfo assemblies)
		{
			this.assemblies = assemblies;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var targetType = (Type)method.currentOp.argument;
			if((targetType.IsInterface || typeof(MonoBehaviour).IsAssignableFrom(targetType)) && Utils.IsUdonAsm(targetType))
			{
				var variable = method.PopStack();
				var outVariable = method.GetOrPushOutVariable(targetType);
				var guidVariable = method.machine.GetConstVariable(assemblies.GetBehaviourInfo(targetType).guid);

				var notMatchLabel = new EmbedAddressLabel();
				var endLabel = new EmbedAddressLabel();
				var checkTypeLabel = new EmbedAddressLabel();
				var checkInheritsLabel = new EmbedAddressLabel();

				var condition = method.GetTmpVariable(typeof(bool)).Reserve();

				variable.Allocate();
				method.machine.AddExtern("SystemObject.__Equals__SystemObject_SystemObject__SystemBoolean",
					condition, variable.OwnType(), method.machine.GetConstVariable(null).OwnType());
				method.machine.AddBranch(condition, checkTypeLabel);
				outVariable.Allocate();
				method.machine.AddCopy(method.machine.GetConstVariable(null), outVariable);
				method.machine.AddJump(endLabel);

				method.machine.ApplyLabel(checkTypeLabel);
				if(!(variable.type == typeof(UdonBehaviour) || Utils.IsUdonAsm(variable.type)))
				{
					var objTypeVariable = method.GetTmpVariable(typeof(Type));
					variable.Allocate();
					method.machine.AddExtern("SystemObject.__GetType__SystemType", objTypeVariable, variable.OwnType());
					method.machine.BinaryOperatorExtern(BinaryOperator.Equality, objTypeVariable,
						method.machine.GetConstVariable(typeof(UdonBehaviour), typeof(Type)), condition);
					method.machine.AddBranch(condition, notMatchLabel);
				}

				var behaviourGuidVariable = method.GetTmpVariable(typeof(Guid));
				variable.Allocate();
				method.machine.GetVariableExtern(variable, AsmTypeInfo.TYPE_ID_NAME, behaviourGuidVariable);
				method.machine.BinaryOperatorExtern(BinaryOperator.Equality, behaviourGuidVariable, guidVariable, condition);
				method.machine.AddBranch(condition, checkInheritsLabel);
				variable.Allocate();
				outVariable.Allocate();
				method.machine.AddCopy(variable, outVariable);
				method.machine.AddJump(endLabel);

				method.machine.ApplyLabel(checkInheritsLabel);
				var inheritsVariable = method.GetTmpVariable(typeof(Guid[]));
				var indexVariable = method.GetTmpVariable(typeof(int));
				variable.Allocate();
				method.machine.GetVariableExtern(variable, AsmTypeInfo.INHERIT_IDS_NAME, inheritsVariable);
				method.machine.AddExtern("SystemArray.__BinarySearch__SystemArray_SystemObject__SystemInt32",
					indexVariable, inheritsVariable.OwnType(), guidVariable.OwnType());
				method.machine.BinaryOperatorExtern(BinaryOperator.GreaterThanOrEqual, indexVariable, method.machine.GetConstVariable((int)0), condition);
				method.machine.AddBranch(condition, notMatchLabel);
				outVariable.Allocate();
				method.machine.AddCopy(variable, outVariable);
				method.machine.AddJump(endLabel);

				method.machine.ApplyLabel(notMatchLabel);
				if(method.currentOp.opCode == OpCodes.Castclass)
				{
					method.machine.ThrowException<InvalidCastException>("Specified cast is not valid.");
				}
				else
				{
					outVariable.Allocate();
					method.machine.AddCopy(method.machine.GetConstVariable(null), outVariable);
				}

				method.machine.ApplyLabel(endLabel);
				condition.Release();
			}
			else
			{
				method.PushStack(new CastedVariable(targetType, method.PopStack()));
			}
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new CastOpcode(modules.GetModule<AssembliesInfo>());
			container.RegisterOpBuilder(OpCodes.Unbox, builder);
			container.RegisterOpBuilder(OpCodes.Unbox_Any, builder);
			container.RegisterOpBuilder(OpCodes.Castclass, builder);
			container.RegisterOpBuilder(OpCodes.Isinst, builder);
		}

		private class CastedVariable : IVariable, IReferenceVariable
		{
			Type IVariable.type => _type;

			string IVariable.name => throw new NotImplementedException();
			uint IAddressLabel.address => throw new NotImplementedException();

			private Type _type;
			private IVariable variable;

			public CastedVariable(Type type, IVariable variable)
			{
				this._type = type;
				this.variable = variable;
			}

			void IVariable.Allocate(int count)
			{
				variable.Allocate(count);
			}

			void IVariable.Use()
			{
				variable.Use();
			}

			IVariable IReferenceVariable.GetValueVariable()
			{
				return variable is IReferenceVariable reference ? reference.GetValueVariable() : variable;
			}

			void IVariable.SetAddress(uint address) { }

			void IReferenceVariable.LoadValue(IUdonProgramBlock block)
			{
				if(variable is IReferenceVariable reference) reference.LoadValue(block);
			}

			void IReferenceVariable.StoreValue(IUdonProgramBlock block)
			{
				if(variable is IReferenceVariable reference) reference.StoreValue(block);
			}
		}
	}
}