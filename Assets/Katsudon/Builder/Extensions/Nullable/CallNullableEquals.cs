using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Katsudon.Builder.Extensions.NullableExtension
{
	[OperationBuilder]
	public class CallNullableEquals : IOperationBuider
	{
		int IOperationBuider.order => 10;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = (MethodInfo)method.currentOp.argument;
			if(methodInfo.IsStatic) return false;
			if(methodInfo.Name != nameof(object.Equals)) return false;
			if(methodInfo.DeclaringType != typeof(object)) return false;

			var variableType = method.PeekStack(1).type;
			if(!variableType.IsGenericType) return false;
			if(variableType.GetGenericTypeDefinition() != typeof(Nullable<>)) return false;

			var b = method.PopStack();
			var a = method.PopStack();

			var outValue = method.GetTmpVariable(typeof(bool));
			method.machine.AddExtern("SystemObject.__Equals__SystemObject_SystemObject__SystemBoolean", outValue, a.OwnType(), b.OwnType());
			method.PushStack(outValue);
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new CallNullableEquals();
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
			modules.AddModule(builder);
		}
	}
}