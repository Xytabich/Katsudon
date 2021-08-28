using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;

namespace Katsudon.Builder.Extensions.UnityExtensions
{
	[OperationBuilder]
	public class IgnoreEditorCall : IOperationBuider
	{
		public int order => 10;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = method.currentOp.argument as MethodInfo;
			var condition = methodInfo.GetCustomAttribute<ConditionalAttribute>();
			if(condition != null && condition.ConditionString == "UNITY_EDITOR")
			{
				int popCount = 0;
				if(!methodInfo.IsStatic) popCount++;
				popCount += methodInfo.GetParameters().Length;
				if(popCount > 0) method.PopMultiple(popCount);
				if(methodInfo.ReturnType != typeof(void))
				{
					method.PushStack(method.GetTmpVariable(methodInfo.ReturnType));
				}
				return true;
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var instance = new IgnoreEditorCall();
			container.RegisterOpBuilder(OpCodes.Call, instance);
			container.RegisterOpBuilder(OpCodes.Callvirt, instance);
		}
	}
}