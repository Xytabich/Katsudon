using System;
using Katsudon.Builder;
using Katsudon.Builder.Externs;

namespace Katsudon.Utility
{
	public struct ForLoop : IDisposable
	{
		private IMethodDescriptor method;
		private ITmpVariable indexVariable;
		private ITmpVariable lengthVariable;
		private IEmbedAddressLabel enterLabel;
		private IEmbedAddressLabel exitLabel;

		private ForLoop(IMethodDescriptor method, ITmpVariable indexVariable, ITmpVariable lengthVariable)
		{
			this.method = method;
			this.indexVariable = indexVariable;
			this.lengthVariable = lengthVariable;

			enterLabel = new EmbedAddressLabel();
			exitLabel = new EmbedAddressLabel();

			method.machine.ApplyLabel(enterLabel);
			var condition = method.GetTmpVariable(typeof(bool));
			method.machine.BinaryOperatorExtern(BinaryOperator.LessThan, indexVariable, lengthVariable, condition);
			method.machine.AddBranch(condition, exitLabel);
		}

		public void Dispose()
		{
			method.machine.BinaryOperatorExtern(BinaryOperator.Addition, indexVariable, method.machine.GetConstVariable((int)1), indexVariable);
			method.machine.AddJump(enterLabel);
			method.machine.ApplyLabel(exitLabel);

			indexVariable.Release();
			lengthVariable.Release();
		}

		public static ForLoop Array(IMethodDescriptor method, IVariable array, out IVariable index)
		{
			var indexVariable = method.GetTmpVariable(typeof(int)).Reserve();
			method.machine.AddCopy(method.machine.GetConstVariable((int)0), indexVariable);

			var length = method.GetTmpVariable(typeof(int)).Reserve();
			method.machine.AddExtern("SystemArray.__get_Length__SystemInt32", length, array.OwnType());

			index = indexVariable;
			return new ForLoop(method, indexVariable, length);
		}
	}
}