using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;
using Katsudon.Info;

namespace Katsudon.Builder.AsmOpCodes
{
	[TypeOperationBuilder]
	public class CallSetter : IOperationBuider
	{
		int IOperationBuider.order => 30;

		private AssembliesInfo assemblies;
		private FieldShortcuts shortcuts;
		private FieldsCollection fieldsCollection;

		private CallSetter(AssembliesInfo assemblies, FieldShortcuts shortcuts, FieldsCollection fieldsCollection)
		{
			this.assemblies = assemblies;
			this.shortcuts = shortcuts;
			this.fieldsCollection = fieldsCollection;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var op = method.currentOp;
			var methodInfo = op.argument as MethodInfo;
			if(methodInfo.IsAbstract || methodInfo.IsVirtual || methodInfo.IsStatic) return false;
			if(methodInfo.ReturnType != typeof(void)) return false;

			FieldInfo field = shortcuts.GetSetter(methodInfo);
			if(field != null)
			{
				var variable = method.PopStack();
				var target = method.PopStack();
				if(target is ThisVariable)
				{
					method.machine.AddCopy(variable, fieldsCollection.GetField(field), field.FieldType);
				}
				else
				{
					method.machine.SetVariableExtern(target, assemblies.GetField(field.DeclaringType, field).name, variable.UseType(field.FieldType));
				}

				return true;
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new CallSetter(modules.GetModule<AssembliesInfo>(), modules.GetModule<FieldShortcuts>(), modules.GetModule<FieldsCollection>());
			container.RegisterOpBuilder(OpCodes.Call, builder);
			modules.AddModule(builder);
		}

		public static void UnRegister(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = modules.GetModule<CallSetter>();
			modules.RemoveModule<CallSetter>();
			container.UnRegisterOpBuilder(OpCodes.Call, builder);
		}
	}
}