using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Katsudon.Builder.Extensions.NullableExtension
{
	[OperationBuilder]
	public class NullableCtor : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var ctorInfo = (ConstructorInfo)method.currentOp.argument;
			if(ctorInfo.IsStatic) return false;
			if(!ctorInfo.DeclaringType.IsGenericType) return false;
			if(ctorInfo.DeclaringType.GetGenericTypeDefinition() != typeof(Nullable<>)) return false;

			var value = method.GetTmpVariable(ctorInfo.DeclaringType);
			value.Allocate();
			method.machine.AddCopy(method.PopStack(), value, ctorInfo.DeclaringType.GetGenericArguments()[0]);
			method.PushStack(value);
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			container.RegisterOpBuilder(OpCodes.Newobj, new NullableCtor());
		}
	}
}