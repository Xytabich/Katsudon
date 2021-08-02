using System;
using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.AsmOpCodes;
using Katsudon.Info;

namespace Katsudon.Builder.Extensions.DelegateExtension
{
	[OperationBuilder]
	public class CtorDelegate : IOperationBuider
	{
		public int order => 0;

		private AssembliesInfo assemblies;

		public CtorDelegate(AssembliesInfo assemblies)
		{
			this.assemblies = assemblies;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var ctorInfo = (ConstructorInfo)method.currentOp.argument;
			if(!typeof(Delegate).IsAssignableFrom(ctorInfo.DeclaringType)) return false;

			var methodPtr = (MethodInfoPtr)method.PeekStack(0);
			if(methodPtr.method.IsStatic) return false;
			if(!Utils.IsUdonAsm(methodPtr.method.DeclaringType)) return false;

			AsmMethodInfo asmMethod = null;
			var methodInfo = methodPtr.method;
			var info = assemblies.GetTypeInfo(methodInfo.DeclaringType);
			if(methodPtr.isVirtual)
			{
				asmMethod = info.GetFamilyMethod(methodInfo);
			}
			if(asmMethod == null) asmMethod = info.GetMethod(methodInfo);
			if(asmMethod != null)
			{
				method.PopStack();
				var target = method.PopStack();

				var actions = method.GetTmpVariable(typeof(Delegate));
				method.machine.AddExtern("SystemObjectArray.__ctor__SystemInt32__SystemObjectArray", actions, method.machine.GetConstVariable((int)1).OwnType());

				var action = method.GetTmpVariable(typeof(object));
				method.machine.AddExtern("SystemArray.__Clone__SystemObject", action,
					method.machine.GetConstVariable(new MethodPattern(asmMethod)).OwnType());
				action.Allocate();
				method.machine.AddExtern("SystemObjectArray.__Set__SystemInt32_SystemObject__SystemVoid", action.OwnType(),
					method.machine.GetConstVariable((int)0).OwnType(), target.OwnType());

				actions.Allocate();
				method.machine.AddExtern("SystemObjectArray.__Set__SystemInt32_SystemObject__SystemVoid", actions.OwnType(),
					method.machine.GetConstVariable((int)0).OwnType(), action.OwnType());

				method.PushStack(actions);
				return true;
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new CtorDelegate(modules.GetModule<AssembliesInfo>());
			container.RegisterOpBuilder(OpCodes.Newobj, builder);
		}
	}
}