using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;
using Katsudon.Info;
using UnityEngine;

namespace Katsudon.Builder.Extensions.UdonExtensions
{
	[OperationBuilder]
	public class CallInstanceBehaviour : IOperationBuider
	{
		public int order => 100;

		private AssembliesInfo assembliesInfo;

		public CallInstanceBehaviour(AssembliesInfo assembliesInfo)
		{
			this.assembliesInfo = assembliesInfo;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = (MethodInfo)method.currentOp.argument;
			if(methodInfo.IsStatic || methodInfo.IsGenericMethod) return false;
			if(!Utils.IsUdonAsmBehaviourOrInterface(methodInfo.DeclaringType)) return false;

			var info = assembliesInfo.GetMethod(methodInfo.DeclaringType, methodInfo);
			if(info != null)
			{
				var target = method.PeekStack(info.parametersName.Length);
				if(methodInfo.ReturnType != typeof(void))
				{
					target.Allocate();
				}

				var parameters = methodInfo.GetParameters();
				int popCount = info.parametersName.Length;
				if(popCount > 0)
				{
					target.Allocate(popCount);
					var iterator = method.PopMultiple(popCount + 1);
					int argIndex = -1;
					while(iterator.MoveNext())
					{
						if(argIndex >= 0)
						{
							method.machine.SetVariableExtern(target, info.parametersName[argIndex], iterator.Current.UseType(parameters[argIndex].ParameterType));
						}
						argIndex++;
					}
				}
				else method.PopStack();

				method.machine.SendEventExtern(target, info.name);

				if(methodInfo.ReturnType != typeof(void))
				{
					method.machine.GetVariableExtern(target, info.returnName, () => method.GetOrPushOutVariable(methodInfo.ReturnType));
				}
				return true;
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new CallInstanceBehaviour(modules.GetModule<AssembliesInfo>());
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
		}
	}
}