using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Info;

namespace Katsudon.Builder.Extensions.Struct
{
	[OperationBuilder]
	public class StructStfld : IOperationBuider
	{
		public int order => 5;

		private AssembliesInfo assemblies;

		private StructStfld(AssembliesInfo assemblies)
		{
			this.assemblies = assemblies;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			if(ILUtils.TryGetLdfld(method.currentOp, out var field))
			{
				if(!Utils.IsUdonAsmStruct(field.DeclaringType)) return false;
				var value = method.PopStack();
				var target = method.PopStack();
				StoreValue(method.machine, target, value, field, assemblies);
				return true;
			}
			return false;
		}

		public static void StoreValue(IUdonMachine machine, IVariable target, IVariable value, FieldInfo field, AssembliesInfo assemblies)
		{
			int index = assemblies.GetStructInfo(field.DeclaringType).GetFieldIndex(field);
			machine.AddExtern("SystemObjectArray.__Set__SystemInt32_SystemObject__SystemVoid",
				target.OwnType(), machine.GetConstVariable((int)(index + StructVariable.FIELDS_OFFSET)).OwnType(), value.OwnType());
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new StructStfld(modules.GetModule<AssembliesInfo>());
			container.RegisterOpBuilder(OpCodes.Stfld, builder);
		}
	}
}