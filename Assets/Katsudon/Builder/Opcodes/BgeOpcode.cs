﻿using System.Reflection.Emit;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class BgeOpcode : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			bool unsigned = method.currentOp.opCode == OpCodes.Bge_Un || method.currentOp.opCode == OpCodes.Bge_Un_S;
			int methodAddress = (int)method.currentOp.argument;

			var b = method.PopStack();
			var a = method.PopStack();

			IVariable variable = null;
			CltOpcode.ProcessOp(method, unsigned, a, b, () => (variable = method.GetTmpVariable(typeof(bool))), out variable);
			method.machine.AddBranch(variable, method.GetMachineAddressLabel(methodAddress));
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new BgeOpcode();
			container.RegisterOpBuilder(OpCodes.Bge, builder);
			container.RegisterOpBuilder(OpCodes.Bge_S, builder);
			container.RegisterOpBuilder(OpCodes.Bge_Un, builder);
			container.RegisterOpBuilder(OpCodes.Bge_Un_S, builder);
		}
	}
}