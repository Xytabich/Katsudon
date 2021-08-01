using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Info;

namespace Katsudon.Builder.AsmOpCodes
{
	[TypeOperationBuilder(1)]
	public class CallLocalVirtual : IOperationBuider
	{
		public int order => 50;

		private AsmTypeInfo typeInfo;
		private CallLocalBehaviour defaultMethodCall;

		private CallLocalVirtual(AsmTypeInfo typeInfo, CallLocalBehaviour defaultMethodCall)
		{
			this.typeInfo = typeInfo;
			this.defaultMethodCall = defaultMethodCall;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = method.currentOp.argument as MethodInfo;
			if(methodInfo.IsStatic) return false;
			if(!Utils.IsUdonAsm(methodInfo.DeclaringType)) return false;

			var target = method.PeekStack(methodInfo.GetParameters().Length);
			if(!(target is ThisVariable))
			{
				return false;
			}

			var familyMethod = typeInfo.GetFamilyMethod(methodInfo);
			return defaultMethodCall.BuildMethod(familyMethod.method, method, method.machine);
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new CallLocalVirtual(modules.GetModule<AsmTypeInfo>(), modules.GetModule<CallLocalBehaviour>());
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
			modules.AddModule(builder);
		}

		public static void UnRegister(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = modules.GetModule<CallLocalVirtual>();
			modules.RemoveModule<CallLocalVirtual>();
			container.UnRegisterOpBuilder(OpCodes.Callvirt, builder);
		}
	}
}