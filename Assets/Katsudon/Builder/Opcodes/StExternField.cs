using System.Collections.Generic;
using System.Reflection.Emit;
using Katsudon.Builder.Helpers;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class StExternField : IOperationBuider
	{
		public int order => 5;

		private IReadOnlyDictionary<FieldIdentifier, FieldNameInfo> externs;

		public StExternField()
		{
			this.externs = UdonCacheHelper.cache.GetFieldNames();
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			if(ILUtils.TryGetStfld(method.currentOp, out var field) &&
				externs.TryGetValue(UdonCacheHelper.cache.GetFieldIdentifier(field), out var nameInfo) &&
				!string.IsNullOrEmpty(nameInfo.setterName))
			{
				var valueVariable = method.PopStack();
				var targetVariable = method.PopStack();

				method.machine.AddExtern(nameInfo.setterName, targetVariable.Mode(VariableMeta.UsageMode.Out), valueVariable.UseType(field.FieldType));
				return true;
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new StExternField();
			container.RegisterOpBuilder(OpCodes.Stfld, builder);
		}
	}
}