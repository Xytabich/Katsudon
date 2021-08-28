using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.Methods;

namespace Katsudon.Builder.Extensions.UdonExtensions
{
	[TypeOperationBuilder]
	public class CallLocalBehaviour : IOperationBuider
	{
		public int order => 90;

		private MethodsInstance methodsContainer;

		private CallLocalBehaviour(MethodsInstance methodsContainer)
		{
			this.methodsContainer = methodsContainer;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = method.currentOp.argument as MethodInfo;
			if(!Utils.IsUdonAsm(methodInfo.DeclaringType)) return false;
			if(!method.isStatic)
			{
				var target = method.PeekStack(methodInfo.GetParameters().Length);
				if(!(target is ThisVariable))
				{
					return false;
				}
			}
			return BuildMethod(methodInfo, method, method.machine);
		}

		public bool BuildMethod(MethodInfo methodInfo, IMethodDescriptor method, IUdonMachine udonMachine)
		{
			var local = methodsContainer.GetDirect(methodInfo);
			if(local != null)
			{
				var retLabel = udonMachine.CreateLabelVariable();
				udonMachine.AddCopy(retLabel, udonMachine.GetReturnAddressGlobal());

				int popCount = 0;
				bool skipTarget = !method.isStatic;
				if(skipTarget) popCount++;
				popCount += methodInfo.GetParameters().Length;
				if(popCount > 0)
				{
					var iterator = method.PopMultiple(popCount);
					int argIndex = 0;
					while(iterator.MoveNext())
					{
						if(skipTarget)
						{
							iterator.Current.Use();
							skipTarget = false;
						}
						else
						{
							var arg = local.arguments[argIndex];
							udonMachine.AddCopy(iterator.Current, arg, arg.type);
							argIndex++;
						}
					}
				}

				udonMachine.AddJump(local);

				(retLabel as IEmbedAddressLabel).Apply();

				if(methodInfo.ReturnType != typeof(void))
				{
					udonMachine.AddCopy(local.ret, () => method.GetOrPushOutVariable(methodInfo.ReturnType, 1));
				}
				return true;
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new CallLocalBehaviour(modules.GetModule<MethodsInstance>());
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
			modules.AddModule(builder);
		}

		public static void UnRegister(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = modules.GetModule<CallLocalBehaviour>();
			modules.RemoveModule<CallLocalBehaviour>();
			container.UnRegisterOpBuilder(OpCodes.Call, builder);
			container.UnRegisterOpBuilder(OpCodes.Callvirt, builder);
		}
	}
}