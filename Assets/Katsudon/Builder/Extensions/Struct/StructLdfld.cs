using System;
using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Info;

namespace Katsudon.Builder.Extensions.Struct
{
	[OperationBuilder]
	public class StructLdfld : IOperationBuider
	{
		public int order => 5;

		private AssembliesInfo assemblies;

		private StructLdfld(AssembliesInfo assemblies)
		{
			this.assemblies = assemblies;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			if(ILUtils.TryGetLdfld(method.currentOp, out var field))
			{
				if(!Utils.IsUdonAsmStruct(field.DeclaringType)) return false;
				var target = method.PopStack();
				if(method.currentOp.opCode == OpCodes.Ldflda)
				{
					method.PushStack(LoadReference(method, target, field, assemblies));
				}
				else
				{
					LoadValue(method.machine, target, field, assemblies, () => method.GetOrPushOutVariable(field.FieldType));
				}
				return true;
			}
			return false;
		}

		public static void LoadValue(IUdonMachine machine, IVariable target, FieldInfo field, AssembliesInfo assemblies, Func<IVariable> outVariable)
		{
			int index = assemblies.GetStructInfo(field.DeclaringType).GetFieldIndex(field);
			machine.AddExtern("SystemObjectArray.__Get__SystemInt32__SystemObject", outVariable, target.OwnType(), machine.GetConstVariable((int)(index + StructVariable.FIELDS_OFFSET)).OwnType());
		}

		private static IVariable LoadReference(IUdonProgramBlock block, IVariable target, FieldInfo field, AssembliesInfo assemblies)
		{
			int index = assemblies.GetStructInfo(field.DeclaringType).GetFieldIndex(field);
			return new ReferenceVariable(block.GetTmpVariable(target.OwnType()).Reserve(),
				block.machine.GetConstVariable((int)(index + StructVariable.FIELDS_OFFSET)), block.GetTmpVariable(field.FieldType));
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new StructLdfld(modules.GetModule<AssembliesInfo>());
			container.RegisterOpBuilder(OpCodes.Ldfld, builder);
			container.RegisterOpBuilder(OpCodes.Ldflda, builder);
		}

		private class ReferenceVariable : IReferenceVariable
		{
			public string name => throw new NotImplementedException();
			public uint address => throw new NotImplementedException();

			public Type type => tmpVariable.type;

			private IVariable fieldIndexConst;
			private ITmpVariable tmpVariable;
			private ITmpVariable targetVariable;

			public ReferenceVariable(ITmpVariable targetVariable, IVariable fieldIndexConst, ITmpVariable tmpVariable)
			{
				this.targetVariable = targetVariable;
				this.fieldIndexConst = fieldIndexConst;
				this.tmpVariable = tmpVariable;

				tmpVariable.onRelease += ReleaseValue;
			}

			public void Use()
			{
				tmpVariable.Use();
			}

			private void ReleaseValue()
			{
				tmpVariable.onRelease -= ReleaseValue;
				targetVariable.Release();
			}

			public void Allocate(int count = 1)
			{
				tmpVariable.Allocate(count);
			}

			public IVariable GetValueVariable() { return tmpVariable; }

			public void LoadValue(IUdonProgramBlock block)
			{
				block.machine.AddExtern("SystemObjectArray.__Get__SystemInt32__SystemObject", tmpVariable, targetVariable.OwnType(), fieldIndexConst.OwnType());
			}

			public void StoreValue(IUdonProgramBlock block)
			{
				tmpVariable.Allocate();
				block.machine.AddExtern("SystemObjectArray.__Set__SystemInt32_SystemObject__SystemVoid", targetVariable.OwnType(), fieldIndexConst.OwnType(), tmpVariable.OwnType());
			}

			void IVariable.SetAddress(uint address)
			{
				throw new NotImplementedException();
			}
		}
	}
}