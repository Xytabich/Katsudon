using System.Reflection;
using System.Reflection.Emit;

namespace Katsudon.Builder.AsmOpCodes
{
	[TypeOperationBuilder]
	public class StLocalField : IOperationBuider
	{
		public int order => 10;

		private FieldsCollection fieldsCollection;

		private StLocalField(FieldsCollection fieldsCollection)
		{
			this.fieldsCollection = fieldsCollection;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			if(method.PeekStack(1) is ThisVariable)
			{
				FieldInfo field;
				if(ILUtils.TryGetStfld(method.currentOp, out field))
				{
					var valueVariable = method.PopStack();
					method.PopStack().Use();
					method.machine.AddCopy(valueVariable, fieldsCollection.GetField(field), field.FieldType);
					return true;
				}
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new StLocalField(modules.GetModule<FieldsCollection>());
			container.RegisterOpBuilder(OpCodes.Stfld, builder);
			modules.AddModule(builder);
		}

		public static void UnRegister(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = modules.GetModule<StLocalField>();
			modules.RemoveModule<StLocalField>();
			container.UnRegisterOpBuilder(OpCodes.Stfld, builder);
		}
	}
}