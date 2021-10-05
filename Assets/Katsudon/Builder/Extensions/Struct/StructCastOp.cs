using System;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;
using Katsudon.Info;

namespace Katsudon.Builder.Extensions.Struct
{
	[OperationBuilder]
	public class StructCastOp : IOperationBuider
	{
		public int order => 50;

		private AssembliesInfo assemblies;

		public StructCastOp(AssembliesInfo assemblies)
		{
			this.assemblies = assemblies;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var targetType = (Type)method.currentOp.argument;
			if(Utils.IsUdonAsmStruct(targetType))
			{
				var variable = method.PopStack();
				var outVariable = method.GetOrPushOutVariable(targetType);
				var typeVariable = method.machine.GetConstVariable(StructVariable.GetStructTypeIdentifier(assemblies.GetStructInfo(targetType).guid));

				var notMatchLabel = new EmbedAddressLabel();
				var endLabel = new EmbedAddressLabel();
				var checkTypeLabel = new EmbedAddressLabel();

				var condition = method.GetTmpVariable(typeof(bool)).Reserve();

				variable.Allocate();
				method.machine.AddExtern("SystemObject.__Equals__SystemObject_SystemObject__SystemBoolean",
					condition, variable.OwnType(), method.machine.GetConstVariable(null).OwnType());
				method.machine.AddBranch(condition, checkTypeLabel);
				outVariable.Allocate();
				method.machine.AddCopy(method.machine.GetConstVariable(null), outVariable);
				method.machine.AddJump(endLabel);

				method.machine.ApplyLabel(checkTypeLabel);
				var objTypeVariable = method.GetTmpVariable(typeof(Type));
				variable.Allocate();
				method.machine.AddExtern("SystemObject.__GetType__SystemType", objTypeVariable, variable.OwnType());
				method.machine.BinaryOperatorExtern(BinaryOperator.Equality, objTypeVariable,
					method.machine.GetConstVariable(typeof(object[]), typeof(Type)), condition);
				method.machine.AddBranch(condition, notMatchLabel);

				var lengthVariable = method.GetTmpVariable(typeof(int));
				variable.Allocate();
				method.machine.AddExtern("SystemArray.__get_Length__SystemInt32", lengthVariable, variable.OwnType());
				method.machine.BinaryOperatorExtern(BinaryOperator.GreaterThanOrEqual, lengthVariable,
					method.machine.GetConstVariable((int)StructVariable.FIELDS_OFFSET), condition);
				method.machine.AddBranch(condition, notMatchLabel);

				var structTypeVariable = method.GetTmpVariable(typeof(string));
				variable.Allocate();
				method.machine.AddExtern("SystemObjectArray.__Get__SystemInt32__SystemObject", structTypeVariable,
					variable.OwnType(), method.machine.GetConstVariable((int)StructVariable.TYPE_INDEX).OwnType());
				method.machine.AddExtern("SystemObject.__Equals__SystemObject_SystemObject__SystemBoolean",
					condition, structTypeVariable.OwnType(), typeVariable.OwnType());
				method.machine.AddBranch(condition, notMatchLabel);
				outVariable.Allocate();
				method.machine.AddCopy(variable, outVariable);
				method.machine.AddJump(endLabel);

				method.machine.ApplyLabel(notMatchLabel);
				if(method.currentOp.opCode == OpCodes.Castclass)
				{
					method.machine.ThrowException<InvalidCastException>("Specified cast is not valid.");
				}
				else
				{
					outVariable.Allocate();
					method.machine.AddCopy(method.machine.GetConstVariable(null), outVariable);
				}

				method.machine.ApplyLabel(endLabel);
				condition.Release();
				return true;
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new StructCastOp(modules.GetModule<AssembliesInfo>());
			container.RegisterOpBuilder(OpCodes.Castclass, builder);
			container.RegisterOpBuilder(OpCodes.Isinst, builder);
		}
	}
}