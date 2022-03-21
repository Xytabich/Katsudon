using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Katsudon.Builder.Extensions.NullableExtension
{
	[OperationBuilder]
	public class CallNullableGetHashCode : IOperationBuider
	{
		int IOperationBuider.order => 10;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = (MethodInfo)method.currentOp.argument;
			if(methodInfo.IsStatic) return false;
			if(methodInfo.Name != nameof(object.GetHashCode)) return false;
			if(methodInfo.DeclaringType != typeof(object)) return false;

			var variableType = method.PeekStack(0).type;
			if(!variableType.IsGenericType) return false;
			if(variableType.GetGenericTypeDefinition() != typeof(Nullable<>)) return false;

			var variable = method.PopStack();
			var isNull = method.GetTmpVariable(typeof(bool));
			variable.Allocate();
			method.machine.AddExtern("SystemObject.__ReferenceEquals__SystemObject_SystemObject__SystemBoolean",
				isNull, variable.OwnType(), method.machine.GetConstVariable(null).OwnType());

			var outValue = method.GetTmpVariable(typeof(int));

			var notNullLabel = new EmbedAddressLabel();
			method.machine.AddBranch(isNull, notNullLabel);

			var endLabel = new EmbedAddressLabel();
			outValue.Allocate();
			method.machine.AddCopy(method.machine.GetConstVariable((int)0), outValue);
			method.machine.AddJump(endLabel);

			method.machine.ApplyLabel(notNullLabel);
			method.machine.AddExtern("SystemObject.__GetHashCode__SystemInt32", outValue, variable.OwnType());

			method.machine.ApplyLabel(endLabel);
			method.PushStack(outValue);
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new CallNullableGetHashCode();
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
			modules.AddModule(builder);
		}
	}
}