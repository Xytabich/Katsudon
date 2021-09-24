using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;
using Katsudon.Info;

namespace Katsudon.Builder.Extensions.DelegateExtension
{
	[TypeOperationBuilder]
	public class CallEventAdd : IOperationBuider
	{
		int IOperationBuider.order => 30;

		private AssembliesInfo assemblies;
		private EventsCollection events;
		private FieldsCollection fieldsCollection;

		private CallEventAdd(AssembliesInfo assemblies, EventsCollection events, FieldsCollection fieldsCollection)
		{
			this.assemblies = assemblies;
			this.events = events;
			this.fieldsCollection = fieldsCollection;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var op = method.currentOp;
			var methodInfo = op.argument as MethodInfo;
			if(methodInfo.IsAbstract || methodInfo.IsVirtual || methodInfo.IsStatic) return false;
			if(methodInfo.ReturnType != typeof(void)) return false;
			if(!Utils.IsUdonAsm(methodInfo.DeclaringType)) return false;
			if(!methodInfo.Name.StartsWith("add_")) return false;

			FieldInfo field = events.GetEventField(methodInfo, true);
			if(field != null)
			{
				var action = method.PopStack();
				var target = method.PopStack();

				if(target is ThisVariable)
				{
					var fieldVariable = fieldsCollection.GetField(field);
					var variable = method.GetTmpVariable(field.FieldType).Reserve();
					CallDelegateCombine.Build(method, fieldVariable, action, variable);
					method.machine.AddCopy(variable, fieldVariable);
					variable.Release();
				}
				else
				{
					var fieldName = assemblies.GetField(field.DeclaringType, field).name;
					var variable = method.GetTmpVariable(field.FieldType).Reserve();
					var fieldVariable = method.GetTmpVariable(field.FieldType);
					target.Allocate();
					method.machine.GetVariableExtern(target, fieldName, fieldVariable);
					CallDelegateCombine.Build(method, fieldVariable, action, variable);
					method.machine.SetVariableExtern(target, fieldName, variable);
					variable.Release();
				}

				return true;
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new CallEventAdd(modules.GetModule<AssembliesInfo>(), modules.GetModule<EventsCollection>(), modules.GetModule<FieldsCollection>());
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
			modules.AddModule(builder);
		}

		public static void UnRegister(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = modules.GetModule<CallEventAdd>();
			modules.RemoveModule<CallEventAdd>();
			container.UnRegisterOpBuilder(OpCodes.Call, builder);
			container.UnRegisterOpBuilder(OpCodes.Callvirt, builder);
		}
	}
}