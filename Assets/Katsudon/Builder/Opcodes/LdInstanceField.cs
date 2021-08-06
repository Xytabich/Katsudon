using System;
using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;
using Katsudon.Info;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class LdInstanceField : IOperationBuider
	{
		public int order => 11;

		private AssembliesInfo assembliesInfo;

		public LdInstanceField(AssembliesInfo assembliesInfo)
		{
			this.assembliesInfo = assembliesInfo;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			FieldInfo field;
			if(ILUtils.TryGetLdfld(method.currentOp, out field))
			{
				var info = assembliesInfo.GetField(field.DeclaringType, field);
				var target = method.PopStack();

				if(method.currentOp.opCode == OpCodes.Ldflda)
				{
					method.PushStack(new ReferenceInstanceVariable(info.name, method.GetTmpVariable(target), method.GetTmpVariable(field.FieldType)));
				}
				else
				{
					method.machine.GetVariableExtern(target, info.name, () => method.GetOrPushOutVariable(field.FieldType));
				}
				return true;
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new LdInstanceField(modules.GetModule<AssembliesInfo>());
			container.RegisterOpBuilder(OpCodes.Ldfld, builder);
			container.RegisterOpBuilder(OpCodes.Ldflda, builder);
		}

		private class ReferenceInstanceVariable : IReferenceVariable
		{
			public string name => throw new NotImplementedException();
			public uint address => throw new NotImplementedException();

			public Type type => tmpVariable.type;

			private string variableName;
			private ITmpVariable tmpVariable;
			private IVariable targetVariable;

			public ReferenceInstanceVariable(string variableName, IVariable targetVariable, ITmpVariable tmpVariable)
			{
				this.variableName = variableName;
				this.targetVariable = targetVariable;
				this.tmpVariable = tmpVariable;

				tmpVariable.onUse += OnValueUse;
				tmpVariable.onRelease += ReleaseValue;
			}

			public void Use()
			{
				targetVariable.Use();
				tmpVariable.Use();
			}

			private void OnValueUse()
			{
				targetVariable.Use();
			}

			private void ReleaseValue()
			{
				tmpVariable.onUse -= OnValueUse;
				tmpVariable.onRelease -= ReleaseValue;
			}

			public void Allocate(int count = 1)
			{
				targetVariable.Allocate(count);
				tmpVariable.Allocate(count);
			}

			public IVariable GetValueVariable() { return tmpVariable; }

			public void LoadValue(IMachineBlock block)
			{
				block.machine.GetVariableExtern(targetVariable, variableName, tmpVariable);
			}

			public void StoreValue(IMachineBlock block)
			{
				block.machine.SetVariableExtern(targetVariable, variableName, tmpVariable);
			}

			void IVariable.SetAddress(uint address)
			{
				throw new NotImplementedException();
			}
		}
	}
}