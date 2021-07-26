using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;
using Katsudon.Info;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class StInstanceField : IOperationBuider
	{
		public int order => 11;

		private AssembliesInfo assembliesInfo;

		public StInstanceField(AssembliesInfo assembliesInfo)
		{
			this.assembliesInfo = assembliesInfo;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			FieldInfo field;
			if(ILUtils.TryGetStfld(method.currentOp, out field))
			{
				var info = assembliesInfo.GetField(field.DeclaringType, field);
				var valueVariable = method.PopStack();
				method.machine.SetVariableExtern(method.PopStack(), info.name, valueVariable.UseType(field.FieldType));
				return true;
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			container.RegisterOpBuilder(OpCodes.Stfld, new StInstanceField(modules.GetModule<AssembliesInfo>()));
		}
	}
}