using Katsudon.Builder.Externs;
using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Katsudon.Builder.Extensions.NullableExtension
{
	[OperationBuilder]
	public class CallNullableHasValue : IOperationBuider
	{
		private const string METHOD_NAME = "get_" + nameof(Nullable<int>.HasValue);

		int IOperationBuider.order => 30;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = (MethodInfo)method.currentOp.argument;
			if(methodInfo.IsStatic) return false;
			if(!methodInfo.DeclaringType.IsGenericType) return false;
			if(methodInfo.DeclaringType.GetGenericTypeDefinition() != typeof(Nullable<>)) return false;
			if(methodInfo.Name != METHOD_NAME) return false;

			var variable = method.PopStack();
			var isNull = method.GetTmpVariable(typeof(bool));
			method.machine.AddExtern("SystemObject.__ReferenceEquals__SystemObject_SystemObject__SystemBoolean",
				isNull, variable.OwnType(), method.machine.GetConstVariable(null).OwnType());

			isNull.Allocate();
			method.machine.UnaryOperatorExtern(UnaryOperator.UnaryNegation, isNull, isNull);

			method.PushStack(isNull);
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new CallNullableHasValue();
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
			modules.AddModule(builder);
		}
	}
}