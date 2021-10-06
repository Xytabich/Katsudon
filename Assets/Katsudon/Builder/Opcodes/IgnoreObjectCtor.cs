using System.Reflection;
using System.Reflection.Emit;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class IgnoreObjectCtor : IOperationBuider
	{
		public int order => 5;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodBase = (MethodBase)method.currentOp.argument;
			if(methodBase.IsStatic) return false;
			if(methodBase.DeclaringType != typeof(object)) return false;
			if(methodBase is ConstructorInfo)
			{
				method.PopStack().Use();
				return true;
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new IgnoreObjectCtor();
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
		}
	}
}