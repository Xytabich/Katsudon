using System;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class CeqOpcode : IOperationBuider
	{
		public int order => 9;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var b = method.PopStack();
			var a = method.PopStack();
			ProcessOp(method, a, b, () => method.GetOrPushOutVariable(typeof(bool)), out var constVariable);
			if(constVariable != null) method.PushStack(constVariable);
			return true;
		}

		public static void ProcessOp(IMethodDescriptor method, IVariable a, IVariable b, Func<IVariable> retVariableCtor, out IVariable constVariable)
		{
			var aCode = NumberCodeUtils.GetCode(a.type);
			var bCode = NumberCodeUtils.GetCode(b.type);

			var ac = a as IConstVariable;
			var bc = b as IConstVariable;
			if(ac != null && bc != null)
			{
				if(NumberCodeUtils.IsPrimitive(aCode) && NumberCodeUtils.IsPrimitive(bCode))
				{
					if(NumberCodeUtils.IsFloat(aCode) || NumberCodeUtils.IsFloat(bCode))
					{
						constVariable = method.machine.GetConstVariable(Convert.ToDouble(ac.value) == Convert.ToDouble(bc.value));
						return;
					}
					else
					{
						if(NumberCodeUtils.IsUnsigned(aCode) && NumberCodeUtils.IsUnsigned(bCode))
						{
							constVariable = method.machine.GetConstVariable(Convert.ToUInt64(ac.value) == Convert.ToUInt64(bc.value));
							return;
						}
						else
						{
							constVariable = method.machine.GetConstVariable(Convert.ToInt64(ac.value) == Convert.ToInt64(bc.value));
							return;
						}
					}
				}
				else
				{
					constVariable = method.machine.GetConstVariable(object.Equals(ac.value, bc.value) || object.Equals(bc.value, ac.value));
					return;
				}
			}
			else
			{
				constVariable = null;
				if(Utils.IsNativeTypeCode(aCode) && Utils.IsNativeTypeCode(bCode))
				{
					if(aCode == bCode)
					{
						method.machine.BinaryOperatorExtern(BinaryOperator.Equality, a.OwnType(), b.OwnType(), typeof(bool), retVariableCtor);
						return;
					}

					if(NumberCodeUtils.IsPrimitive(aCode) && NumberCodeUtils.IsPrimitive(bCode))
					{
						Type type = a.type;
						if(aCode != bCode && bc == null && (ac != null || NumberCodeUtils.GetSize(bCode) > NumberCodeUtils.GetSize(aCode)))
						{
							type = b.type;
						}
						method.machine.BinaryOperatorExtern(BinaryOperator.Equality, a.UseType(type), b.UseType(type), typeof(bool), retVariableCtor);
						return;
					}
				}

				method.machine.AddExtern(
					"SystemObject.__Equals__SystemObject_SystemObject__SystemBoolean",
					retVariableCtor, a.OwnType(), b.OwnType()
				);
			}
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new CeqOpcode();
			container.RegisterOpBuilder(OpCodes.Ceq, builder);
			modules.AddModule(builder);
		}
	}
}