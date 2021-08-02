using System;
using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;
using Katsudon.Utility;

namespace Katsudon.Builder.Extensions.DelegateExtension
{
	[OperationBuilder]
	public class CallDelegateInvoke : IOperationBuider
	{
		private const int TARGET_OFFSET = 0;
		private const int METHOD_NAME_OFFSET = 1;
		private const int ARGUMENTS_OFFSET = 2;

		public int order => 15;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = method.currentOp.argument as MethodInfo;
			if(methodInfo.Name == nameof(Action.Invoke) && typeof(Delegate).IsAssignableFrom(methodInfo.DeclaringType))
			{
				var parameters = methodInfo.GetParameters();

				//Struct: object[][]{{target, methodName[, ...args][, returnName}}
				var actions = method.GetTmpVariable(method.PeekStack(parameters.Length)).Reserve();
				using(ForLoop.Array(method, actions, out var index))
				{
					var action = method.GetTmpVariable(typeof(object)).Reserve();
					method.machine.AddExtern("SystemObjectArray.__Get__SystemInt32__SystemObject", action, actions.OwnType(), index.OwnType());

					var target = ElementGetter(method, action, TARGET_OFFSET).Reserve();

					int argsCount = parameters.Length;
					if(argsCount > 0)
					{
						var iterator = method.PopMultiple(argsCount);
						int argIndex = 0;
						while(iterator.MoveNext())
						{
							method.machine.SetVariableExtern(target, ElementGetter(method, action, argIndex + ARGUMENTS_OFFSET),
								method.GetTmpVariable(iterator.Current));
							argIndex++;
						}
					}
					method.PopStack();

					method.machine.SendEventExtern(target, ElementGetter(method, action, METHOD_NAME_OFFSET));

					if(methodInfo.ReturnType != typeof(void))
					{
						method.machine.GetVariableExtern(target, ElementGetter(method, action, argsCount + ARGUMENTS_OFFSET),
							() => method.GetOrPushOutVariable(methodInfo.ReturnType));
					}

					action.Release();
					target.Release();
				}
				actions.Release();

				return true;
			}
			return false;
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