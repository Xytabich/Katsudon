using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;
using Katsudon.Info;

namespace Katsudon.Builder.AsmOpCodes
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
			var methodInfo = method.currentOp.argument as MethodInfo;
			if(!Utils.IsUdonAsm(methodInfo.DeclaringType)) return false;
			var info = assembliesInfo.GetMethod(methodInfo.DeclaringType, methodInfo);
			if(info != null)
			{
				var target = method.PeekStack(info.parametersName.Length);
				if(methodInfo.ReturnType != typeof(void))
				{
					target.Allocate();
				}

				int popCount = info.parametersName.Length;
				if(popCount > 0)
				{
					target.Allocate(popCount);
					var iterator = method.PopMultiple(popCount);
					int argIndex = 0;
					while(iterator.MoveNext())
					{
						method.machine.SetVariableExtern(target, info.parametersName[argIndex], iterator.Current);
						argIndex++;
					}
				}
				method.PopStack();

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