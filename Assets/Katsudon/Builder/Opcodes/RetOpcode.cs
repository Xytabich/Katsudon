using System.Reflection.Emit;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class RetOpcode : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var retVariable = method.GetReturnVariable();
			if(retVariable != null && !method.stackIsEmpty)
			{
				method.machine.AddCopy(method.PopStack(), retVariable, retVariable.type);
			}
			method.machine.AddJump(method.GetReturnAddress());
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			container.RegisterOpBuilder(OpCodes.Ret, new RetOpcode());
		}
	}
}