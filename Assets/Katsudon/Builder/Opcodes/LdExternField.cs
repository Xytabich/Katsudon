using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Katsudon.Builder.Helpers;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class LdExternField : IOperationBuider
	{
		public int order => 5;

		private IReadOnlyDictionary<FieldIdentifier, FieldNameInfo> externs;

		public LdExternField()
		{
			this.externs = UdonCacheHelper.cache.GetFieldNames();
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			if(ILUtils.TryGetLdfld(method.currentOp, out var field))
			{
				var fieldId = UdonCacheHelper.cache.GetFieldIdentifier(field);
				if(externs.TryGetValue(fieldId, out var nameInfo) && !string.IsNullOrEmpty(nameInfo.getterName))
				{
					var target = method.PopStack();
					if(method.currentOp.opCode == OpCodes.Ldflda && !string.IsNullOrEmpty(nameInfo.setterName))
					{
						method.PushStack(new ReferenceExternVariable(nameInfo.getterName, nameInfo.setterName,
							method.GetTmpVariable(target), method.GetTmpVariable(field.FieldType)));
					}
					else
					{
						method.machine.AddExtern(nameInfo.getterName, () => method.GetOrPushOutVariable(field.FieldType), target.OwnType());
					}
					return true;
				}
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new LdExternField();
			container.RegisterOpBuilder(OpCodes.Ldfld, builder);
			container.RegisterOpBuilder(OpCodes.Ldflda, builder);
		}

		private class ReferenceExternVariable : IReferenceVariable
		{
			public string name => throw new NotImplementedException();
			public uint address => throw new NotImplementedException();

			public Type type => tmpVariable.type;

			private string loadName;
			private string storeName;
			private ITmpVariable tmpVariable;
			private IVariable targetVariable;

			public ReferenceExternVariable(string loadName, string storeName, IVariable targetVariable, ITmpVariable tmpVariable)
			{
				this.loadName = loadName;
				this.storeName = storeName;
				this.tmpVariable = tmpVariable;
				this.targetVariable = targetVariable;

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

			public void LoadValue(IMethodDescriptor method)
			{
				method.machine.AddExtern(loadName, tmpVariable, targetVariable.OwnType());
			}

			public void StoreValue(IMethodDescriptor method)
			{
				method.machine.AddExtern(storeName, targetVariable.Mode(VariableMeta.UsageMode.Out), tmpVariable.OwnType());
			}

			void IVariable.SetAddress(uint address)
			{
				throw new NotImplementedException();
			}
		}
	}
}