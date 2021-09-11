using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Katsudon.Builder.Exceptions
{
	[OperationBuilder]
	public class CtorNotSupportException : IOperationBuider
	{
		public int order => 10000;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var ctorInfo = method.currentOp.argument as ConstructorInfo;
			throw new Exception(string.Format("Constructor {0} declared in {1} is not supported by udon", ctorInfo, ctorInfo.DeclaringType));
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new CtorNotSupportException();
			container.RegisterOpBuilder(OpCodes.Newobj, builder);
		}
	}
}