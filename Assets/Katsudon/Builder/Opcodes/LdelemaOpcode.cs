using System;
using System.Reflection.Emit;
using Katsudon.Builder.Variables;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class LdelemaOpcode : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var index = method.PopStack();
			var array = method.PopStack();

			var elementType = array.type.GetElementType();
			var arrType = ArrayTypes.GetUdonArrayType(array.type);
			method.PushStack(new ReferenceElementVariable(
				Utils.GetExternName(arrType, "__Get__SystemInt32__{0}", arrType.GetElementType()),
				Utils.GetExternName(arrType, "__Set__SystemInt32_{0}__SystemVoid", arrType.GetElementType()),
				method.GetTmpVariable(elementType),
				method.GetTmpVariable(array.OwnType()).Reserve(),
				method.GetReadonlyVariable(index.UseType(typeof(int)))
			));

			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new LdelemaOpcode();
			container.RegisterOpBuilder(OpCodes.Ldelema, builder);
		}

		private class ReferenceElementVariable : IReferenceVariable
		{
			public string name => throw new NotImplementedException();
			public uint address => throw new NotImplementedException();

			public Type type => tmpVariable.type;

			private string loadName;
			private string storeName;
			private ITmpVariable arrayVariable;
			private IVariable indexVariable;
			private ITmpVariable tmpVariable;

			public ReferenceElementVariable(string loadName, string storeName, ITmpVariable tmpVariable, ITmpVariable arrayVariable, IVariable indexVariable)
			{
				this.loadName = loadName;
				this.storeName = storeName;
				this.tmpVariable = tmpVariable;
				this.arrayVariable = arrayVariable;
				this.indexVariable = indexVariable;
				if(indexVariable is ITmpVariable tmp) tmp.Reserve();
				tmpVariable.onRelease += ReleaseValue;
			}

			public void Use()
			{
				tmpVariable.Use();
			}

			private void ReleaseValue()
			{
				tmpVariable.onRelease -= ReleaseValue;
				arrayVariable.Release();
				if(indexVariable is ITmpVariable tmp) tmp.Release();
			}

			public void Allocate(int count = 1)
			{
				tmpVariable.Allocate(count);
			}

			public IVariable GetValueVariable() { return tmpVariable; }

			public void LoadValue(IUdonProgramBlock block)
			{
				block.machine.AddExtern(loadName, tmpVariable, arrayVariable.OwnType(), indexVariable.UseType(typeof(int)));
			}

			public void StoreValue(IUdonProgramBlock block)
			{
				block.machine.AddExtern(storeName, arrayVariable.OwnType(), indexVariable.UseType(typeof(int)), tmpVariable.OwnType());
			}

			void IVariable.SetAddress(uint address)
			{
				throw new NotImplementedException();
			}
		}
	}
}