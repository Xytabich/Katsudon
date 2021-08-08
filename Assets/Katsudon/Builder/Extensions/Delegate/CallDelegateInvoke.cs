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
		private const int TARGET_OFFSET = 0;
		private const int METHOD_NAME_OFFSET = 1;
		private const int ARGUMENTS_OFFSET = 2;

		public int order => 15;

		private List<ITmpVariable> argumentsCache = null;//TODO: cache

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = method.currentOp.argument as MethodInfo;
			if(methodInfo.Name == nameof(Action.Invoke) && typeof(Delegate).IsAssignableFrom(methodInfo.DeclaringType))
			{
				var parameters = methodInfo.GetParameters();

				int argsCount = parameters.Length;
				if(argsCount != 0)
				{
					if(argumentsCache == null) argumentsCache = new List<ITmpVariable>();
					var iterator = method.PopMultiple(argsCount);
					while(iterator.MoveNext())
					{
						argumentsCache.Add(method.GetTmpVariable(iterator.Current).Reserve());
					}
				}

				//Struct: object[][]{{target, methodName[, ...args][, returnName}}
				var actions = method.GetTmpVariable(method.PopStack()).Reserve();

				var outVariable = methodInfo.ReturnType == typeof(void) ? null : method.GetOrPushOutVariable(methodInfo.ReturnType);

				using(var loop = ForLoop.Array(method, actions, out var index))
				{
					var action = method.GetTmpVariable(typeof(object)).Reserve();
					method.machine.AddExtern("SystemObjectArray.__Get__SystemInt32__SystemObject", action, actions.OwnType(), index.OwnType());

					var target = ElementGetter(method, action, TARGET_OFFSET).Reserve();

					if(argsCount != 0)
					{
						for(int i = 0; i < argumentsCache.Count; i++)
						{
							method.machine.SetVariableExtern(target, ElementGetter(method, action, i + ARGUMENTS_OFFSET), argumentsCache[i]);
						}
					}

					method.machine.SendEventExtern(target, ElementGetter(method, action, METHOD_NAME_OFFSET));

					if(outVariable != null)
					{
						method.machine.GetVariableExtern(target, ElementGetter(method, action, argsCount + ARGUMENTS_OFFSET), outVariable);
					}

					action.Release();
					target.Release();
				}
				actions.Release();
				if(argsCount != 0)
				{
					for(int i = 0; i < argumentsCache.Count; i++)
					{
						argumentsCache[i].Release();
					}
					argumentsCache.Clear();
				}

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