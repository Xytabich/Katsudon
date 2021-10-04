using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;
using Katsudon.Info;

namespace Katsudon.Builder.Extensions.PropertiesShortcuts
{
	[TypeOperationBuilder]
	public class CallGetter : IOperationBuider
	{
		int IOperationBuider.order => 30;

		private AssembliesInfo assemblies;
		private FieldShortcuts shortcuts;
		private FieldsCollection fieldsCollection;

		private CallGetter(AssembliesInfo assemblies, FieldShortcuts shortcuts, FieldsCollection fieldsCollection)
		{
			this.assemblies = assemblies;
			this.shortcuts = shortcuts;
			this.fieldsCollection = fieldsCollection;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = (MethodInfo)method.currentOp.argument;
			if(methodInfo.IsAbstract || methodInfo.IsVirtual || methodInfo.IsStatic) return false;
			if(methodInfo.ReturnType == typeof(void)) return false;
			if(!Utils.IsUdonAsmBehaviour(methodInfo.DeclaringType)) return false;

			FieldInfo field = shortcuts.GetGetter(methodInfo);
			if(field != null)
			{
				var target = method.PopStack();
				if(target is ThisVariable)
				{
					target.Use();
					method.machine.AddCopy(fieldsCollection.GetField(field), () => method.GetOrPushOutVariable(methodInfo.ReturnType, 1));
				}
				else
				{
					method.machine.GetVariableExtern(target, assemblies.GetField(field.DeclaringType, field).name,
						() => method.GetOrPushOutVariable(methodInfo.ReturnType));
				}
				return true;
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new CallGetter(modules.GetModule<AssembliesInfo>(), modules.GetModule<FieldShortcuts>(), modules.GetModule<FieldsCollection>());
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
			modules.AddModule(builder);
		}

		public static void UnRegister(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = modules.GetModule<CallGetter>();
			modules.RemoveModule<CallGetter>();
			container.UnRegisterOpBuilder(OpCodes.Call, builder);
			container.UnRegisterOpBuilder(OpCodes.Callvirt, builder);
		}
	}
}