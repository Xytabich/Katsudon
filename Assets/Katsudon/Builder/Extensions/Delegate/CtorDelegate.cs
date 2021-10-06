using System;
using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.AsmOpCodes;
using Katsudon.Builder.Helpers;
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
			if(Utils.IsUdonAsmBehaviourOrInterface(methodPtr.method.DeclaringType))
			{
				if(methodPtr.method.IsStatic) return false;

				AsmMethodInfo asmMethod = null;
				var methodInfo = methodPtr.method;
				var info = assemblies.GetBehaviourInfo(methodInfo.DeclaringType);
				if(methodPtr.isVirtual)
				{
					asmMethod = info.GetFamilyMethod(methodInfo);
				}
				if(asmMethod == null) asmMethod = info.GetMethod(methodInfo);
				if(asmMethod != null)
				{
					method.PopStack();
					var target = method.PopStack();
					BuildDelegate(method, method.machine.GetConstVariable(new UdonMethodPattern(asmMethod)), target);
					return true;
				}
			}
			else if(typeof(Delegate).IsAssignableFrom(methodPtr.method.DeclaringType))
			{
				method.PopStack();
				var target = method.PopStack();
				method.machine.AddExtern("SystemArray.__Clone__SystemObject", () => method.GetOrPushOutVariable(typeof(Delegate)), target.OwnType());
				return true;
			}
			else if(UdonCacheHelper.cache.TryFindUdonMethod(method.PeekStack(1).type, methodPtr.method, out var methodId, out var fullName))
			{
				method.PopStack();
				var target = method.PopStack();
				BuildDelegate(method, method.machine.GetConstVariable(new ExternMethodPattern(fullName, methodPtr.method.IsStatic, methodId)), target);
				return true;
			}
			return false;
		}

		private static void BuildDelegate(IMethodDescriptor method, IVariable pattern, IVariable target = null)
		{
			var actions = method.GetTmpVariable(typeof(Delegate));
			method.machine.AddExtern("SystemObjectArray.__ctor__SystemInt32__SystemObjectArray", actions, method.machine.GetConstVariable((int)1).OwnType());

			var action = method.GetTmpVariable(typeof(object));
			method.machine.AddExtern("SystemArray.__Clone__SystemObject", action, pattern.OwnType());

			if(target != null)
			{
				action.Allocate();
				method.machine.AddExtern("SystemObjectArray.__Set__SystemInt32_SystemObject__SystemVoid", action.OwnType(),
					method.machine.GetConstVariable((int)DelegateUtility.TARGET_OFFSET).OwnType(), target.OwnType());
			}

			actions.Allocate();
			method.machine.AddExtern("SystemObjectArray.__Set__SystemInt32_SystemObject__SystemVoid", actions.OwnType(),
				method.machine.GetConstVariable((int)0).OwnType(), action.OwnType());

			method.PushStack(actions);
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new CtorDelegate(modules.GetModule<AssembliesInfo>());
			container.RegisterOpBuilder(OpCodes.Newobj, builder);
		}
	}
}