using System;
using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;
using Katsudon.Utility;

namespace Katsudon.Builder.Extensions.DelegateExtension
{
	[OperationBuilder]
	public class CallDelegateCombine : IOperationBuider
	{
		public int order => 15;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = (MethodInfo)method.currentOp.argument;
			if(methodInfo.Name == nameof(Delegate.Combine) && typeof(Delegate).IsAssignableFrom(methodInfo.DeclaringType))
			{
				var parameters = methodInfo.GetParameters();
				if(parameters.Length == 1)
				{
					/*
					int counter = 0;
					for(int i = 0; i < delegates.Length; i++)
					{
						if(delegates[i] != null) counter += delegates[i].Length;
					}
					if(counter == 0) return null;
					var newEvt = new object[counter];
					counter = 0;
					for(int i = 0; i < delegates.Length; i++)
					{
						if(delegates[i] != null)
						{
							int length = delegates[i].Length;
							delegates[i].CopyTo(newEvt, counter);
							counter += length;
						}
					}
					return newEvt;
					*/
					var delegates = method.PopStack();

					var endLabel = new EmbedAddressLabel();
					var buildLabel = new EmbedAddressLabel();

					IVariable condition;
					var outVariable = method.GetTmpVariable(typeof(Delegate));
					outVariable.Allocate();
					outVariable.Reserve();

					var counter = method.GetTmpVariable(typeof(int)).Reserve();
					method.machine.AddCopy(method.machine.GetConstVariable((int)0), counter);

					delegates.Allocate();
					using(ForLoop.Array(method, delegates, out var index))
					{
						var element = method.GetTmpVariable(typeof(object)).Reserve();
						delegates.Allocate();
						method.machine.AddExtern("SystemObjectArray.__Get__SystemInt32__SystemObject", element, delegates.OwnType(), index.OwnType());

						condition = method.GetTmpVariable(typeof(bool));
						method.machine.ObjectEquals(condition, element, method.machine.GetConstVariable(null));

						var addCounterLabel = new EmbedAddressLabel();
						method.machine.AddBranch(condition, addCounterLabel);
						var continueLabel = new EmbedAddressLabel();
						method.machine.AddJump(continueLabel);

						method.machine.ApplyLabel(addCounterLabel);
						var tmpLen = method.GetTmpVariable(typeof(int));
						method.machine.AddExtern("SystemArray.__get_Length__SystemInt32", tmpLen, element.OwnType());
						method.machine.BinaryOperatorExtern(BinaryOperator.Addition, counter, tmpLen, counter);

						element.Release();
						method.machine.ApplyLabel(continueLabel);
					}

					condition = method.GetTmpVariable(typeof(bool));
					method.machine.BinaryOperatorExtern(BinaryOperator.Equality, counter, method.machine.GetConstVariable((int)0), condition);
					method.machine.AddBranch(condition, buildLabel);
					method.machine.AddCopy(method.machine.GetConstVariable(null), outVariable);
					method.machine.AddJump(endLabel);

					method.machine.ApplyLabel(buildLabel);
					method.machine.AddExtern("SystemObjectArray.__ctor__SystemInt32__SystemObjectArray", outVariable, counter.OwnType());
					method.machine.AddCopy(method.machine.GetConstVariable((int)0), counter);

					delegates.Allocate();
					using(ForLoop.Array(method, delegates, out var index))
					{
						var element = method.GetTmpVariable(typeof(object)).Reserve();
						method.machine.AddExtern("SystemObjectArray.__Get__SystemInt32__SystemObject", element, delegates.OwnType(), index.OwnType());

						condition = method.GetTmpVariable(typeof(bool));
						method.machine.ObjectEquals(condition, element, method.machine.GetConstVariable(null));

						var addDelegateLabel = new EmbedAddressLabel();
						method.machine.AddBranch(condition, addDelegateLabel);
						var continueLabel = new EmbedAddressLabel();
						method.machine.AddJump(continueLabel);

						method.machine.ApplyLabel(addDelegateLabel);
						method.machine.AddExtern("SystemArray.__CopyTo__SystemArray_SystemInt32__SystemVoid",
							element.OwnType(), outVariable.OwnType(), counter.OwnType());

						var tmpLen = method.GetTmpVariable(typeof(int));
						method.machine.AddExtern("SystemArray.__get_Length__SystemInt32", tmpLen, element.OwnType());
						method.machine.BinaryOperatorExtern(BinaryOperator.Addition, counter, tmpLen, counter);

						element.Release();
						method.machine.ApplyLabel(continueLabel);
					}

					counter.Release();
					method.machine.ApplyLabel(endLabel);

					outVariable.Release();
					method.PushStack(outVariable);
				}
				else
				{
					/*
					if(delegateB == null) return delegateA;
					if(delegateA == null) return delegateB;
					int length = delegateA.Length;
					var newEvt = new object[length + delegateB.Length];
					delegateA.CopyTo(newEvt, 0);
					delegateB.CopyTo(newEvt, length);
					return newEvt;
					*/
					var b = method.PopStack();
					var a = method.PopStack();
					var outVariable = method.GetTmpVariable(typeof(Delegate));
					outVariable.Allocate();
					outVariable.Reserve();

					Build(method, a, b, outVariable);

					outVariable.Release();
					method.PushStack(outVariable);
				}
				return true;
			}
			return false;
		}

		public static void Build(IMethodDescriptor method, IVariable a, IVariable b, IVariable outVariable)
		{
			var endLabel = new EmbedAddressLabel();
			var checkALabel = new EmbedAddressLabel();
			var buildNewLabel = new EmbedAddressLabel();

			var condition = method.GetTmpVariable(typeof(bool));
			b.Allocate();
			method.machine.ObjectEquals(condition, b, method.machine.GetConstVariable(null));
			method.machine.AddBranch(condition, checkALabel);
			a.Allocate();
			outVariable.Allocate();
			method.machine.AddCopy(a, outVariable);
			method.machine.AddJump(endLabel);

			method.machine.ApplyLabel(checkALabel);
			condition = method.GetTmpVariable(typeof(bool));
			a.Allocate();
			method.machine.ObjectEquals(condition, a, method.machine.GetConstVariable(null));
			method.machine.AddBranch(condition, buildNewLabel);
			b.Allocate();
			outVariable.Allocate();
			method.machine.AddCopy(b, outVariable);
			method.machine.AddJump(endLabel);

			method.machine.ApplyLabel(buildNewLabel);
			var length = method.GetTmpVariable(typeof(int)).Reserve();
			a.Allocate();
			method.machine.AddExtern("SystemArray.__get_Length__SystemInt32", length, a.OwnType());
			var fullLen = method.GetTmpVariable(typeof(int)).Reserve();
			b.Allocate();
			method.machine.AddExtern("SystemArray.__get_Length__SystemInt32", fullLen, b.OwnType());

			method.machine.BinaryOperatorExtern(BinaryOperator.Addition, length, fullLen, fullLen);
			method.machine.AddExtern("SystemObjectArray.__ctor__SystemInt32__SystemObjectArray", outVariable, fullLen.OwnType());
			outVariable.Allocate();
			method.machine.AddExtern("SystemArray.__CopyTo__SystemArray_SystemInt32__SystemVoid", a.OwnType(), outVariable.OwnType(), method.machine.GetConstVariable((int)0).OwnType());
			outVariable.Allocate();
			method.machine.AddExtern("SystemArray.__CopyTo__SystemArray_SystemInt32__SystemVoid", b.OwnType(), outVariable.OwnType(), length.OwnType());

			length.Release();
			fullLen.Release();

			method.machine.ApplyLabel(endLabel);
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new CallDelegateCombine();
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
		}
	}
}