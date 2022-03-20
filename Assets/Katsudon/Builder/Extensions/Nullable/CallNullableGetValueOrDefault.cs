using Katsudon.Builder.Externs;
using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Katsudon.Builder.Extensions.NullableExtension
{
	[OperationBuilder]
	public class CallNullableGetValueOrDefault : IOperationBuider
	{
		private const string METHOD_NAME = nameof(Nullable<int>.GetValueOrDefault);

		int IOperationBuider.order => 30;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = (MethodInfo)method.currentOp.argument;
			if(methodInfo.IsStatic) return false;
			if(!methodInfo.DeclaringType.IsGenericType) return false;
			if(methodInfo.DeclaringType.GetGenericTypeDefinition() != typeof(Nullable<>)) return false;
			if(methodInfo.Name != METHOD_NAME) return false;

			IVariable defaultVariable = null;
			if(methodInfo.GetParameters().Length == 1)
			{
				defaultVariable = method.PopStack();
			}

			var variable = method.PopStack();
			var isNull = method.GetTmpVariable(typeof(bool));
			variable.Allocate();
			method.machine.AddExtern("SystemObject.__ReferenceEquals__SystemObject_SystemObject__SystemBoolean",
				isNull, variable.OwnType(), method.machine.GetConstVariable(null).OwnType());

			var type = methodInfo.DeclaringType.GetGenericArguments()[0];
			var outValue = method.GetTmpVariable(type);

			var notNullLabel = new EmbedAddressLabel();
			method.machine.AddBranch(isNull, notNullLabel);

			var endLabel = new EmbedAddressLabel();
			if(defaultVariable == null)
			{
				defaultVariable = method.machine.GetConstVariable(Activator.CreateInstance(type));
			}
			outValue.Allocate();
			method.machine.AddCopy(defaultVariable, outValue);
			method.machine.AddJump(endLabel);

			method.machine.ApplyLabel(notNullLabel);
			outValue.Allocate();
			method.machine.AddCopy(variable, outValue);

			method.machine.ApplyLabel(endLabel);
			method.PushStack(outValue);
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new CallNullableGetValueOrDefault();
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
			modules.AddModule(builder);
		}
	}
}