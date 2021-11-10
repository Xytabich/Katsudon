using System;
using System.Reflection.Emit;

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
			CltOpcode.ProcessOp(method, null, indexVariable, method.machine.GetConstVariable(addresses.Length),
				() => (condition = method.GetTmpVariable(typeof(bool))), out condition);
			method.machine.AddBranch(condition, outLabel);

			indexVariable.Allocate();
			CgeOpcode.ProcessOp(method, null, indexVariable, method.machine.GetConstVariable(0),
				() => (condition = method.GetTmpVariable(typeof(bool))), out condition);
			method.machine.AddBranch(condition, outLabel);

			IVariable addressVariable = null;
			method.machine.AddExtern(
				"SystemUInt32Array.__Get__SystemInt32__SystemUInt32",
				() => (addressVariable = method.GetTmpVariable(typeof(uint))),
				new LabelList((method.machine as IRawUdonMachine).mainMachine.GetConstCollection(),
					Array.ConvertAll(addresses, a => method.GetMachineAddressLabel(a))).OwnType(),
				indexVariable.UseType(typeof(int))
			);
			method.machine.AddJump(addressVariable);

			method.machine.ApplyLabel(outLabel);
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			container.RegisterOpBuilder(OpCodes.Switch, new SwitchOpcode());
		}

		private class LabelList : IVariable, IDeferredValue<IVariable>
		{
			public string name => throw new NotImplementedException();
			public uint address => throw new NotImplementedException();
			public Type type => typeof(uint[]);

			private ConstCollection constCollection;
			private IAddressLabel[] labels;

			public LabelList(ConstCollection constCollection, IAddressLabel[] labels)
			{
				this.constCollection = constCollection;
				this.labels = labels;
			}

			public void Allocate(int count = 1) { }

			public void Use() { }

			IVariable IDeferredValue<IVariable>.GetValue()
			{
				return constCollection.GetConstVariable(Array.ConvertAll(labels, l => l.address)).Used();
			}

			void IVariable.SetAddress(uint address)
			{
				throw new NotImplementedException();
			}
		}
	}
}