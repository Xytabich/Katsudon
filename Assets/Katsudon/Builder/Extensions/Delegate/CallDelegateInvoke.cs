using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;
using Katsudon.Utility;

namespace Katsudon.Builder.Extensions.DelegateExtension
{
	[OperationBuilder]
	public class CallDelegateInvoke : IOperationBuider
	{
		public int order => 15;

		private List<VariableMeta> argumentsCache = new List<VariableMeta>();//TODO: cache
		private List<ITmpVariable> reservedCache = new List<ITmpVariable>();

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = method.currentOp.argument as MethodInfo;
			if(methodInfo.Name == nameof(Action.Invoke) && typeof(Delegate).IsAssignableFrom(methodInfo.DeclaringType))
			{
				var parameters = methodInfo.GetParameters();

				int argsCount = parameters.Length;
				if(argsCount != 0)
				{
					var iterator = method.PopMultiple(argsCount);
					int index = 0;
					while(iterator.MoveNext())
					{
						var parameter = iterator.Current;
						var type = parameters[index].ParameterType;
						if(type.IsByRef) type = type.GetElementType();
						else
						{
							parameter = method.GetTmpVariable(parameter).Reserve();
							reservedCache.Add((ITmpVariable)parameter);
						}
						argumentsCache.Add(parameter.UseType(type));
						index++;
					}
				}

				//Struct: object[][]{{target, methodName, delegateType[, ...args][, returnName]}}
				var actions = method.GetTmpVariable(method.PopStack()).Reserve();

				var outVariable = methodInfo.ReturnType == typeof(void) ? null : method.GetOrPushOutVariable(methodInfo.ReturnType);

				using(var loop = ForLoop.Array(method, actions, out var index))
				{
					var action = method.GetTmpVariable(typeof(object)).Reserve();
					method.machine.AddExtern("SystemObjectArray.__Get__SystemInt32__SystemObject", action, actions.OwnType(), index.OwnType());

					var udonCallLabel = new EmbedAddressLabel();
					var externLabel = new EmbedAddressLabel();
					var externStaticLabel = new EmbedAddressLabel();

					var startLabel = method.machine.CreateLabelVariable();
					var jumpAddress = ElementGetter(method, action, DelegateUtility.DELEGATE_TYPE_OFFSET).Reserve();
					AddAddressOperation(method.machine, BinaryOperator.Multiplication, jumpAddress, method.machine.GetConstVariable((uint)sizeof(uint)));
					AddAddressOperation(method.machine, BinaryOperator.Addition, jumpAddress, startLabel);
					method.machine.AddJump(jumpAddress);
					jumpAddress.Release();
					(startLabel as IEmbedAddressLabel).Apply();
					method.machine.AddJump(udonCallLabel);// DelegateUtility.TYPE_UDON_BEHAVIOUR
					method.machine.AddJump(externLabel);// DelegateUtility.TYPE_EXTERN
					method.machine.AddJump(externStaticLabel);// DelegateUtility.TYPE_STATIC_EXTERN

					var endLabel = new EmbedAddressLabel();
					#region udon method call
					{
						method.machine.ApplyLabel(udonCallLabel);
						var target = ElementGetter(method, action, DelegateUtility.TARGET_OFFSET).Reserve();
						for(int i = 0; i < argsCount; i++)
						{
							method.machine.SetVariableExtern(target, ElementGetter(method, action, i + DelegateUtility.ARGUMENTS_OFFSET), argumentsCache[i]);
						}

						method.machine.SendEventExtern(target, ElementGetter(method, action, DelegateUtility.METHOD_NAME_OFFSET));

						if(outVariable != null)
						{
							method.machine.GetVariableExtern(target, ElementGetter(method, action, argsCount + DelegateUtility.ARGUMENTS_OFFSET), outVariable);
						}
						target.Release();
						method.machine.AddJump(endLabel);
					}
					#endregion
					#region extern method call
					{
						method.machine.ApplyLabel(externLabel);

						var rawMachine = method.machine as IRawUdonMachine;

						var target = ElementGetter(method, action, DelegateUtility.TARGET_OFFSET).Reserve();
						rawMachine.AddPush(target.OwnType());

						method.machine.ApplyLabel(externStaticLabel);

						for(int i = 0; i < argsCount; i++)
						{
							rawMachine.AddPush(argumentsCache[i]);
						}

						if(outVariable != null)
						{
							outVariable.Allocate();
							rawMachine.AddPush(outVariable.OwnType());
						}

						var nameVariable = ElementGetter(method, action, DelegateUtility.METHOD_NAME_OFFSET);
						nameVariable.Use();
						rawMachine.mainMachine.AddOpcode(VRC.Udon.VM.Common.OpCode.EXTERN, nameVariable);

						rawMachine.ApplyReferences();
						target.Release();
					}
					#endregion

					method.machine.ApplyLabel(endLabel);
					action.Release();
				}
				actions.Release();
				if(argsCount != 0)
				{
					argumentsCache.Clear();
					for(int i = 0; i < reservedCache.Count; i++)
					{
						reservedCache[i].Release();
					}
					reservedCache.Clear();
				}

				return true;
			}
			return false;
		}

		private static void AddAddressOperation(IUdonMachine machine, BinaryOperator op, IVariable inOutVariable, IVariable rightVariable)
		{
			machine.AddExtern(BinaryOperatorExtension.GetExternName(op, typeof(uint), typeof(uint), typeof(uint)),
				inOutVariable, inOutVariable.OwnType(), rightVariable.OwnType());
		}

		private static ITmpVariable ElementGetter(IMethodDescriptor method, IVariable action, int index)
		{
			var variable = method.GetTmpVariable(typeof(object));
			method.machine.AddExtern("SystemObjectArray.__Get__SystemInt32__SystemObject", variable, action.OwnType(), method.machine.GetConstVariable(index).OwnType());
			return variable;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new CallDelegateInvoke();
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
		}
	}
}