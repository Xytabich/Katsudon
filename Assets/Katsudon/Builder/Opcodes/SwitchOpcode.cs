using System;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class SwitchOpcode : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			int[] addresses = (int[])method.currentOp.argument;

			var indexVariable = method.PopStack();

			var outLabel = new EmbedAddressLabel();
			IVariable condition = null;

			indexVariable.Allocate();
			if(!NumberCodeUtils.IsUnsigned(Type.GetTypeCode(indexVariable.type)))
			{
				CgeOpcode.ProcessOp(method, null, indexVariable, method.machine.GetConstVariable(0),
					() => (condition = method.GetTmpVariable(typeof(bool))), out condition);
				method.machine.AddBranch(condition, outLabel);
			}

			var addressVariable = method.GetTmpVariable(typeof(uint)).Reserve();
			method.machine.AddExtern(ConvertExtension.GetExternName(typeof(object), typeof(uint)), addressVariable, indexVariable.OwnType());

			CltOpcode.ProcessOp(method, null, addressVariable, method.machine.GetConstVariable((uint)addresses.Length),
				() => (condition = method.GetTmpVariable(typeof(bool))), out condition);
			method.machine.AddBranch(condition, outLabel);

			method.machine.BinaryOperatorExtern(BinaryOperator.Multiplication, addressVariable, method.machine.GetConstVariable((uint)(sizeof(uint) * 2)), addressVariable);
			uint counter = method.machine.GetAddressCounter();
			// counter + PUSH PUSH PUSH EXTERN JUMP_INDIRECT (10 words)
			var startAddress = method.machine.GetConstVariable(counter + (uint)(sizeof(uint) * 10));
			method.machine.BinaryOperatorExtern(BinaryOperator.Addition, addressVariable, startAddress, addressVariable);
			method.machine.AddJump(addressVariable);
			for(int i = 0; i < addresses.Length; i++)
			{
				method.machine.AddJump(method.GetMachineAddressLabel(addresses[i]));
			}

			addressVariable.Release();
			method.machine.ApplyLabel(outLabel);
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			container.RegisterOpBuilder(OpCodes.Switch, new SwitchOpcode());
		}
	}
}