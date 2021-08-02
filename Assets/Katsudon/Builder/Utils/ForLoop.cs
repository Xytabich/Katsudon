using System;
using Katsudon.Builder;
using Katsudon.Builder.Externs;

namespace Katsudon.Utility
{
	public struct ForLoop : IDisposable
	{
		public readonly IEmbedAddressLabel continueLabel;
		public readonly IEmbedAddressLabel breakLabel;

		private IMethodDescriptor method;
		private ITmpVariable indexVariable;
		private ITmpVariable lengthVariable;

		private ForLoop(IMethodDescriptor method, ITmpVariable indexVariable, ITmpVariable lengthVariable)
		{
			this.method = method;
			this.indexVariable = indexVariable;
			this.lengthVariable = lengthVariable;

			continueLabel = new EmbedAddressLabel();
			breakLabel = new EmbedAddressLabel();

			method.machine.ApplyLabel(continueLabel);
			var condition = method.GetTmpVariable(typeof(bool));
			method.machine.BinaryOperatorExtern(BinaryOperator.LessThan, indexVariable, lengthVariable, condition);
			method.machine.AddBranch(condition, breakLabel);
		}

		public void Dispose()
		{
			method.machine.BinaryOperatorExtern(BinaryOperator.Addition, indexVariable, method.machine.GetConstVariable((int)1), indexVariable);
			method.machine.AddJump(continueLabel);
			method.machine.ApplyLabel(breakLabel);

			indexVariable.Release();
			lengthVariable.Release();
		}

		/// <remarks>WARNING: The loop uses a temporary bool variable, if you need to reserve a variable with the same type, this must be done BEFORE the loop, otherwise the value may be overwritten.</remarks>
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

	public struct ReverseForLoop : IDisposable
	{
		public readonly IEmbedAddressLabel continueLabel;
		public readonly IEmbedAddressLabel breakLabel;

		private IMethodDescriptor method;
		private ITmpVariable indexVariable;

		private ReverseForLoop(IMethodDescriptor method, ITmpVariable indexVariable)
		{
			this.method = method;
			this.indexVariable = indexVariable;

			continueLabel = new EmbedAddressLabel();
			breakLabel = new EmbedAddressLabel();

			method.machine.ApplyLabel(continueLabel);
			var condition = method.GetTmpVariable(typeof(bool));
			method.machine.BinaryOperatorExtern(BinaryOperator.GreaterThanOrEqual, indexVariable, method.machine.GetConstVariable((int)0), condition);
			method.machine.AddBranch(condition, breakLabel);
		}

		public void Dispose()
		{
			method.machine.BinaryOperatorExtern(BinaryOperator.Subtraction, indexVariable, method.machine.GetConstVariable((int)1), indexVariable);
			method.machine.AddJump(continueLabel);
			method.machine.ApplyLabel(breakLabel);

			indexVariable.Release();
		}

		/// <remarks>WARNING: The loop uses a temporary bool variable, if you need to reserve a variable with the same type, this must be done BEFORE the loop, otherwise the value may be overwritten.</remarks>
		public static ReverseForLoop Array(IMethodDescriptor method, IVariable array, out IVariable index)
		{
			var indexVariable = method.GetTmpVariable(typeof(int)).Reserve();
			method.machine.AddExtern("SystemArray.__get_Length__SystemInt32", indexVariable, array.OwnType());
			method.machine.BinaryOperatorExtern(BinaryOperator.Subtraction, indexVariable, method.machine.GetConstVariable((int)1), indexVariable);

			index = indexVariable;
			return new ReverseForLoop(method, indexVariable);
		}

		/// <remarks>WARNING: The loop uses a temporary bool variable, if you need to reserve a variable with the same type, this must be done BEFORE the loop, otherwise the value may be overwritten.</remarks>
		public static ReverseForLoop Length(IMethodDescriptor method, IVariable length, out IVariable index)
		{
			var indexVariable = method.GetTmpVariable(typeof(int)).Reserve();
			method.machine.AddCopy(length, indexVariable);
			method.machine.BinaryOperatorExtern(BinaryOperator.Subtraction, indexVariable, method.machine.GetConstVariable((int)1), indexVariable);

			index = indexVariable;
			return new ReverseForLoop(method, indexVariable);
		}
	}
}