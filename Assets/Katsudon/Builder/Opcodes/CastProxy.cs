using System;
using System.Reflection.Emit;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class CastProxy : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			method.PushStack(new CastedVariable((Type)method.currentOp.argument, method.PopStack()));
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new CastProxy();
			container.RegisterOpBuilder(OpCodes.Unbox, builder);
			container.RegisterOpBuilder(OpCodes.Unbox_Any, builder);
			container.RegisterOpBuilder(OpCodes.Castclass, builder);
			container.RegisterOpBuilder(OpCodes.Isinst, builder);
		}

		private class CastedVariable : IVariable, IReferenceVariable
		{
			Type IVariable.type => _type;

			string IVariable.name => throw new NotImplementedException();
			uint IAddressLabel.address => throw new NotImplementedException();

			private Type _type;
			private IVariable variable;

			public CastedVariable(Type type, IVariable variable)
			{
				this._type = type;
				this.variable = variable;
			}

			void IVariable.Allocate(int count)
			{
				variable.Allocate();
			}

			void IVariable.Use()
			{
				variable.Use();
			}

			IVariable IReferenceVariable.GetValueVariable()
			{
				return variable is IReferenceVariable reference ? reference.GetValueVariable() : variable;
			}

			void IVariable.SetAddress(uint address) { }

			void IReferenceVariable.LoadValue(IUdonProgramBlock block)
			{
				if(variable is IReferenceVariable reference) reference.LoadValue(block);
			}

			void IReferenceVariable.StoreValue(IUdonProgramBlock block)
			{
				if(variable is IReferenceVariable reference) reference.StoreValue(block);
			}
		}
	}
}