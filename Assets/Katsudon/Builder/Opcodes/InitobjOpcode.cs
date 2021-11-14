using System;
using System.Reflection.Emit;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class InitobjExternOpcode : IOperationBuider
	{
		public int order => 100;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var type = (Type)method.currentOp.argument;
			if(type.IsValueType)
			{
				method.machine.AddCopy(method.machine.GetConstVariable(Activator.CreateInstance(type)), method.PopStack());
				return true;
			}
			else
			{
				method.machine.AddCopy(method.machine.GetConstVariable(null), method.PopStack());
				return true;
			}
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new InitobjExternOpcode();
			container.RegisterOpBuilder(OpCodes.Initobj, builder);
		}
	}
}