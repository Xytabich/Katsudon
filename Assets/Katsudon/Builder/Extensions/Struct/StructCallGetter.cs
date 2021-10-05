using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.Extensions.PropertiesShortcuts;
using Katsudon.Info;

namespace Katsudon.Builder.Extensions.Struct
{
	[OperationBuilder]
	public class StructCallGetter : IOperationBuider
	{
		int IOperationBuider.order => 30;

		private AssembliesInfo assemblies;
		private FieldShortcuts shortcuts;

		private StructCallGetter(AssembliesInfo assemblies, FieldShortcuts shortcuts)
		{
			this.assemblies = assemblies;
			this.shortcuts = shortcuts;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = (MethodInfo)method.currentOp.argument;
			if(methodInfo.IsAbstract || methodInfo.IsStatic) return false;
			if(methodInfo.ReturnType == typeof(void)) return false;
			if(!Utils.IsUdonAsmStruct(methodInfo.DeclaringType)) return false;

			FieldInfo field = shortcuts.GetGetter(methodInfo);
			if(field != null)
			{
				var target = method.PopStack();
				StructLdfld.LoadValue(method.machine, target, field, assemblies, () => method.GetOrPushOutVariable(field.FieldType));
				return true;
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new StructCallGetter(modules.GetModule<AssembliesInfo>(), modules.GetModule<FieldShortcuts>());
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
		}
	}
}