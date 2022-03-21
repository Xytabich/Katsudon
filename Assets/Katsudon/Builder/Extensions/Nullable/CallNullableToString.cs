using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Katsudon.Builder.Extensions.NullableExtension
{
	[OperationBuilder]
	public class CallNullableToString : IOperationBuider
	{
		int IOperationBuider.order => 10;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = (MethodInfo)method.currentOp.argument;
			if(methodInfo.IsStatic) return false;
			if(methodInfo.Name != nameof(object.ToString)) return false;
			if(methodInfo.DeclaringType != typeof(object)) return false;

			var variableType = method.PeekStack(0).type;
			if(!variableType.IsGenericType) return false;
			if(variableType.GetGenericTypeDefinition() != typeof(Nullable<>)) return false;

			var outValue = method.GetTmpVariable(typeof(string));
			method.machine.AddExtern("SystemConvert.__ToString__SystemObject__SystemString", outValue, method.PopStack().OwnType());
			method.PushStack(outValue);
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new CallNullableToString();
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
			modules.AddModule(builder);
		}
	}
}