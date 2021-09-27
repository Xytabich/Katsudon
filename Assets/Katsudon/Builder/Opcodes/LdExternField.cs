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
							target, method.GetTmpVariable(field.FieldType)));
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
			private bool ignoreOnUse = false;

			public ReferenceExternVariable(string loadName, string storeName, IVariable targetVariable, ITmpVariable tmpVariable)
			{
				this.loadName = loadName;
				this.storeName = storeName;
				this.tmpVariable = tmpVariable;
				this.targetVariable = targetVariable;

				tmpVariable.onUse += OnUse;
				tmpVariable.onRelease += OnRelease;
			}

			public void Use()
			{
				tmpVariable.Use();
			}

			public void Allocate(int count = 1)
			{
				tmpVariable.Allocate(count);
				targetVariable.Allocate(count);
			}

			public IVariable GetValueVariable() { return tmpVariable; }

			public void LoadValue(IUdonProgramBlock block)
			{
				targetVariable.Allocate();
				ignoreOnUse = true;
				block.machine.AddExtern(loadName, tmpVariable, targetVariable.OwnType());
				ignoreOnUse = false;
			}

			public void StoreValue(IUdonProgramBlock block)
			{
				tmpVariable.Allocate();
				targetVariable.Allocate();
				ignoreOnUse = true;
				block.machine.AddExtern(storeName, targetVariable.Mode(VariableMeta.UsageMode.OutOnly | VariableMeta.UsageMode.Out), tmpVariable.OwnType());
				ignoreOnUse = false;
			}

			void IVariable.SetAddress(uint address)
			{
				throw new NotImplementedException();
			}

			private void OnUse()
			{
				if(ignoreOnUse) return;
				targetVariable.Use();
			}

			private void OnRelease()
			{
				tmpVariable.onUse -= OnUse;
				tmpVariable.onRelease -= OnRelease;
			}
		}
	}
}