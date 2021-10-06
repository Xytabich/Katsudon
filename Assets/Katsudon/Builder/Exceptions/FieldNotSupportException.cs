using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Katsudon.Builder.Exceptions
{
	[OperationBuilder]
	public class FieldNotSupportException : IOperationBuider
	{
		public int order => 10000;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var fieldInfo = (FieldInfo)method.currentOp.argument;
			throw new Exception(string.Format("Field {0} declared in {1} is not supported by udon", fieldInfo, fieldInfo.DeclaringType));
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new FieldNotSupportException();
			container.RegisterOpBuilder(OpCodes.Stfld, builder);
			container.RegisterOpBuilder(OpCodes.Stsfld, builder);
			container.RegisterOpBuilder(OpCodes.Ldfld, builder);
			container.RegisterOpBuilder(OpCodes.Ldflda, builder);
			container.RegisterOpBuilder(OpCodes.Ldsfld, builder);
			container.RegisterOpBuilder(OpCodes.Ldsflda, builder);
		}
	}
}