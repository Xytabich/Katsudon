using System;
using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;
using Katsudon.Info;

namespace Katsudon.Builder.Extensions.UdonExtensions
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
					method.PushStack(new ReferenceInstanceVariable(info.name, method.GetTmpVariable(target.OwnType()).Reserve(), method.GetTmpVariable(field.FieldType)));
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
			private ITmpVariable targetVariable;

			public ReferenceInstanceVariable(string variableName, ITmpVariable targetVariable, ITmpVariable tmpVariable)
			{
				this.variableName = variableName;
				this.targetVariable = targetVariable;
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
				block.machine.GetVariableExtern(targetVariable, variableName, tmpVariable);
			}

			public void StoreValue(IUdonProgramBlock block)
			{
				tmpVariable.Allocate();
				block.machine.SetVariableExtern(targetVariable, variableName, tmpVariable);
			}

			void IVariable.SetAddress(uint address)
			{
				throw new NotImplementedException();
			}
		}
	}
}