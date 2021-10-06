using System;
using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;
using Katsudon.Utility;

namespace Katsudon.Builder.Extensions.DelegateExtension
{
	[OperationBuilder]
	public class CallDelegateRemove : IOperationBuider
	{
		public int order => 15;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = (MethodInfo)method.currentOp.argument;
			if((methodInfo.Name == nameof(Delegate.Remove) || methodInfo.Name == nameof(Delegate.RemoveAll)) &&
				typeof(Delegate).IsAssignableFrom(methodInfo.DeclaringType))
			{
				var actions = method.PopStack();
				var removeFrom = method.PopStack();
				var outVariable = method.GetTmpVariable(typeof(Delegate));
				outVariable.Allocate();
				outVariable.Reserve();

				Build(method, methodInfo.Name == nameof(Delegate.RemoveAll), removeFrom, actions, outVariable);

				outVariable.Release();
				method.PushStack(outVariable);
				return true;
			}
			return false;
		}

		public static void Build(IMethodDescriptor method, bool removeAll, IVariable removeFrom, IVariable actions, IVariable outVariable)
		{
			/*
			if(removeFrom == null || actions == removeFrom) return null;
			if(actions == null) return removeFrom;
			int remainingLength = removeFrom.Length;
			for(int i = 0; i < actions.Length; i++)
			{
				var action = actions[0];
				for(int j = remainingLength-1; j >= 0; j--)
				{
					var evt = removeFrom[j];
					if(evt[TARGET_OFFSET] != action[TARGET_OFFSET]) continue;
					if(evt[METHOD_NAME_OFFSET] != action[METHOD_NAME_OFFSET]) continue;

					remainingLength--;
					Array.Copy(removeFrom, j+1, removeFrom, j, remainingLength-j);
#if !REMOVE_ALL
					break;
#endif
				}
			}
			if(remainingLength == 0) return null;
			if(remainingLength == removeFrom.Length) return removeFrom;

			var newEvt = new object[];
			Array.Copy(removeFrom, newEvt, remainingLength);
			return newEvt;
			*/
			var endLabel = new EmbedAddressLabel();
			var checkSelfLabel = new EmbedAddressLabel();
			var checkActionsLabel = new EmbedAddressLabel();
			var countLabel = new EmbedAddressLabel();
			var checkLengthLabel = new EmbedAddressLabel();
			var buildLabel = new EmbedAddressLabel();

			var condition = method.GetTmpVariable(typeof(bool));
			removeFrom.Allocate();
			method.machine.ObjectEquals(condition, removeFrom, method.machine.GetConstVariable(null));
			method.machine.AddBranch(condition, checkSelfLabel);
			outVariable.Allocate();
			method.machine.AddCopy(method.machine.GetConstVariable(null), outVariable);
			method.machine.AddJump(endLabel);

			method.machine.ApplyLabel(checkSelfLabel);
			condition = method.GetTmpVariable(typeof(bool));
			removeFrom.Allocate();
			actions.Allocate();
			method.machine.ObjectEquals(condition, removeFrom, actions);
			method.machine.AddBranch(condition, checkActionsLabel);
			outVariable.Allocate();
			method.machine.AddCopy(method.machine.GetConstVariable(null), outVariable);
			method.machine.AddJump(endLabel);

			method.machine.ApplyLabel(checkActionsLabel);
			condition = method.GetTmpVariable(typeof(bool));
			actions.Allocate();
			method.machine.ObjectEquals(condition, actions, method.machine.GetConstVariable(null));
			method.machine.AddBranch(condition, countLabel);
			removeFrom.Allocate();
			outVariable.Allocate();
			method.machine.AddCopy(removeFrom, outVariable);
			method.machine.AddJump(endLabel);

			method.machine.ApplyLabel(countLabel);
			var remainingLength = method.GetTmpVariable(typeof(int)).Reserve();
			removeFrom.Allocate();
			method.machine.AddExtern("SystemArray.__get_Length__SystemInt32", remainingLength, removeFrom.OwnType());
			actions.Allocate();
			using(ForLoop.Array(method, actions, out var actionIndex))
			{
				var action = method.GetTmpVariable(typeof(object)).Reserve();
				method.machine.AddExtern("SystemObjectArray.__Get__SystemInt32__SystemObject", action, actions.OwnType(), actionIndex.OwnType());
				using(var loop = ReverseForLoop.Length(method, remainingLength, out var index))
				{
					var evt = method.GetTmpVariable(typeof(object)).Reserve();
					removeFrom.Allocate();
					method.machine.AddExtern("SystemObjectArray.__Get__SystemInt32__SystemObject", evt, removeFrom.OwnType(), index.OwnType());

					var continueLabel = new EmbedAddressLabel();

					// if(evt[TARGET_OFFSET] != action[TARGET_OFFSET]) continue;
					var valueA = method.GetTmpVariable(typeof(object));
					var valueB = method.GetTmpVariable(typeof(object));
					condition = method.GetTmpVariable(typeof(bool));
					method.machine.AddExtern("SystemObjectArray.__Get__SystemInt32__SystemObject", valueA, evt.OwnType(), method.machine.GetConstVariable(DelegateUtility.TARGET_OFFSET).OwnType());
					method.machine.AddExtern("SystemObjectArray.__Get__SystemInt32__SystemObject", valueB, action.OwnType(), method.machine.GetConstVariable(DelegateUtility.TARGET_OFFSET).OwnType());
					method.machine.ObjectEquals(condition, valueA, valueB);
					method.machine.AddBranch(condition, continueLabel);

					// if(evt[METHOD_NAME_OFFSET] != action[METHOD_NAME_OFFSET]) continue;
					valueA = method.GetTmpVariable(typeof(object));
					valueB = method.GetTmpVariable(typeof(object));
					condition = method.GetTmpVariable(typeof(bool));
					method.machine.AddExtern("SystemObjectArray.__Get__SystemInt32__SystemObject", valueA, evt.OwnType(), method.machine.GetConstVariable(DelegateUtility.METHOD_NAME_OFFSET).OwnType());
					method.machine.AddExtern("SystemObjectArray.__Get__SystemInt32__SystemObject", valueB, action.OwnType(), method.machine.GetConstVariable(DelegateUtility.METHOD_NAME_OFFSET).OwnType());
					method.machine.ObjectEquals(condition, valueA, valueB);
					method.machine.AddBranch(condition, continueLabel);

					//remainingLength--;
					method.machine.BinaryOperatorExtern(BinaryOperator.Subtraction, remainingLength, method.machine.GetConstVariable((int)1), remainingLength);

					//Array.Copy(removeFrom, j+1, removeFrom, j, remainingLength-j);
					removeFrom.Allocate(2);
					var copyFrom = method.GetTmpVariable(typeof(int));
					var copyCount = method.GetTmpVariable(typeof(int));
					method.machine.BinaryOperatorExtern(BinaryOperator.Addition, index, method.machine.GetConstVariable((int)1), copyFrom);
					method.machine.BinaryOperatorExtern(BinaryOperator.Subtraction, remainingLength, index, copyCount);
					method.machine.AddExtern("SystemArray.__Copy__SystemArray_SystemInt32_SystemArray_SystemInt32_SystemInt32__SystemVoid",
						removeFrom.OwnType(), copyFrom.OwnType(), removeFrom.OwnType(), index.OwnType(), copyCount.OwnType());

					if(!removeAll)
					{
						method.machine.AddJump(loop.breakLabel);
					}
					method.machine.ApplyLabel(continueLabel);

					evt.Release();
				}
				action.Release();
			}

			//if(remainingLength == 0) return null;
			condition = method.GetTmpVariable(typeof(bool));
			method.machine.BinaryOperatorExtern(BinaryOperator.Equality, remainingLength, method.machine.GetConstVariable((int)0), condition);
			method.machine.AddBranch(condition, checkLengthLabel);
			outVariable.Allocate();
			method.machine.AddCopy(method.machine.GetConstVariable(null), outVariable);
			method.machine.AddJump(endLabel);

			method.machine.ApplyLabel(checkLengthLabel);
			//if(remainingLength == removeFrom.Length) return removeFrom;
			var length = method.GetTmpVariable(typeof(int));
			removeFrom.Allocate();
			method.machine.AddExtern("SystemArray.__get_Length__SystemInt32", length, removeFrom.OwnType());
			condition = method.GetTmpVariable(typeof(bool));
			method.machine.BinaryOperatorExtern(BinaryOperator.Equality, remainingLength, length, condition);
			method.machine.AddBranch(condition, buildLabel);
			removeFrom.Allocate();
			outVariable.Allocate();
			method.machine.AddCopy(removeFrom, outVariable);
			method.machine.AddJump(endLabel);

			method.machine.ApplyLabel(buildLabel);
			method.machine.AddExtern("SystemObjectArray.__ctor__SystemInt32__SystemObjectArray", outVariable, remainingLength.OwnType());
			outVariable.Allocate();
			method.machine.AddExtern("SystemArray.__Copy__SystemArray_SystemArray_SystemInt32__SystemVoid",
				removeFrom.OwnType(), outVariable.OwnType(), remainingLength.OwnType());

			remainingLength.Release();

			method.machine.ApplyLabel(endLabel);
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new CallDelegateRemove();
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
		}
	}
}