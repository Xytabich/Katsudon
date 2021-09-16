using System.Reflection;
using System.Reflection.Emit;

namespace Katsudon.Builder.Extensions.UdonExtensions
{
	[TypeOperationBuilder]
	public class LdLocalField : IOperationBuider
	{
		public int order => 10;

		private FieldsCollection fieldsCollection;

		private LdLocalField(FieldsCollection fieldsCollection)
		{
			this.fieldsCollection = fieldsCollection;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			if(method.PeekStack(0) is ThisVariable)
			{
				FieldInfo field;
				if(ILUtils.TryGetLdfld(method.currentOp, out field))
				{
					method.PopStack().Use();
					method.PushStack(fieldsCollection.GetField(field), true);
					return true;
				}
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new LdLocalField(modules.GetModule<FieldsCollection>());
			container.RegisterOpBuilder(OpCodes.Ldfld, builder);
			container.RegisterOpBuilder(OpCodes.Ldflda, builder);
			modules.AddModule(builder);
		}

		public static void UnRegister(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = modules.GetModule<LdLocalField>();
			modules.RemoveModule<LdLocalField>();
			container.UnRegisterOpBuilder(OpCodes.Ldfld, builder);
			container.UnRegisterOpBuilder(OpCodes.Ldflda, builder);
		}
	}
}