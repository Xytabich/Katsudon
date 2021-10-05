using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.Extensions.PropertiesShortcuts;
using Katsudon.Info;

namespace Katsudon.Builder.Extensions.Struct
{
	[OperationBuilder]
	public class StructCallSetter : IOperationBuider
	{
		int IOperationBuider.order => 30;

		private AssembliesInfo assemblies;
		private FieldShortcuts shortcuts;

		private StructCallSetter(AssembliesInfo assemblies, FieldShortcuts shortcuts)
		{
			this.assemblies = assemblies;
			this.shortcuts = shortcuts;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = (MethodInfo)method.currentOp.argument;
			if(methodInfo.IsAbstract || methodInfo.IsStatic) return false;
			if(methodInfo.ReturnType != typeof(void)) return false;
			if(!Utils.IsUdonAsmStruct(methodInfo.DeclaringType)) return false;

			FieldInfo field = shortcuts.GetSetter(methodInfo);
			if(field != null)
			{
				var value = method.PopStack();
				var target = method.PopStack();
				StructStfld.StoreValue(method.machine, target, value, field, assemblies);
				return true;
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new StructCallSetter(modules.GetModule<AssembliesInfo>(), modules.GetModule<FieldShortcuts>());
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
		}
	}
}