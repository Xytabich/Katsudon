using System;
using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Info;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class LdftnOpcode : IOperationBuider
	{
		int IOperationBuider.order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = (MethodInfo)method.currentOp.argument;

			method.PushStack(new MethodInfoPtr(methodInfo, method.currentOp.opCode == OpCodes.Ldvirtftn));
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new LdftnOpcode();
			container.RegisterOpBuilder(OpCodes.Ldftn, builder);
			container.RegisterOpBuilder(OpCodes.Ldvirtftn, builder);
		}
	}

	public class MethodInfoPtr : IVariable
	{
		public readonly MethodInfo method;
		public readonly bool isVirtual;

		string IVariable.name => throw new NotImplementedException();
		Type IVariable.type => throw new NotImplementedException();
		uint IAddressLabel.address => throw new NotImplementedException();

		public MethodInfoPtr(MethodInfo method, bool isVirtual)
		{
			this.method = method;
			this.isVirtual = isVirtual;
		}

		void IVariable.Allocate(int count)
		{
			throw new NotImplementedException();
		}

		void IVariable.SetAddress(uint address)
		{
			throw new NotImplementedException();
		}

		void IVariable.Use()
		{
			throw new NotImplementedException();
		}
	}
}