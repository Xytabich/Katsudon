using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Info;
using UnityEngine;

namespace Katsudon.Builder.Extensions.UnityExtensions
{
	[OperationBuilder]
	public class CallTryGetComponent : IOperationBuider
	{
		public int order => 15;

		private AssembliesInfo assemblies;

		private CallTryGetComponent(AssembliesInfo assemblies)
		{
			this.assemblies = assemblies;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = method.currentOp.argument as MethodInfo;
			if(methodInfo.Name == nameof(Component.TryGetComponent) &&
				(methodInfo.DeclaringType == typeof(Component) || methodInfo.DeclaringType == typeof(GameObject)))
			{
				var componentOutVariable = method.PopStack();
				IVariable typeVariable = null;
				if(!methodInfo.IsGenericMethod)
				{
					typeVariable = method.PopStack();
				}
				var targetVariable = method.PopStack();
				CallGetComponent.BuildGetComponent(method, assemblies, methodInfo, nameof(Component.GetComponent),
					targetVariable, typeVariable, null, _ => componentOutVariable);
				method.machine.AddExtern("UnityEngineObject.__op_Inequality__UnityEngineObject_UnityEngineObject__SystemBoolean",
					() => method.GetOrPushOutVariable(typeof(bool)), componentOutVariable.OwnType(), method.machine.GetConstVariable(null).OwnType());
				return true;
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new CallTryGetComponent(modules.GetModule<AssembliesInfo>());
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
		}
	}
}