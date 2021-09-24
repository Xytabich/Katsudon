using System.ComponentModel;
using System.Reflection;
using System.Reflection.Emit;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class InlineLocalCall : IOperationBuider
	{
		public int order => 80;

		private MethodBodyBuilder bodyBuilder;

		public InlineLocalCall(MethodBodyBuilder bodyBuilder)
		{
			this.bodyBuilder = bodyBuilder;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = method.currentOp.argument as MethodInfo;
			if(methodInfo.IsStatic || methodInfo.IsGenericMethod) return false;
			if(!Utils.IsUdonAsm(methodInfo.DeclaringType)) return false;
			if((methodInfo.MethodImplementationFlags & MethodImplAttributes.AggressiveInlining) == 0) return false;

			var parameters = methodInfo.GetParameters();
			var target = method.PeekStack(parameters.Length);
			if(!(target is ThisVariable))
			{
				for(int i = 0; i < parameters.Length; i++)
				{
					if(parameters[i].ParameterType.IsByRef)
					{
						throw new System.Exception("Method with reference parameters can only be called locally");
					}
				}
				return false;
			}

			var reserved = CollectionCache.GetList<ITmpVariable>();
			var arguments = CollectionCache.GetList<IVariable>();
			int argsCount = parameters.Length;
			if(argsCount != 0)
			{
				var iterator = method.PopMultiple(argsCount + 1);
				ReadOnlyAttribute readOnly;
				int index = -1;
				while(iterator.MoveNext())
				{
					if(index >= 0)
					{
						var parameter = iterator.Current;
						if(!parameters[index].ParameterType.IsByRef)
						{
							if((readOnly = parameters[index].GetCustomAttribute<ReadOnlyAttribute>()) != null && readOnly.IsReadOnly)
							{
								parameter = method.GetReadonlyVariable(parameter.UseType(parameters[index].ParameterType));
								if(parameter is ITmpVariable tmp) reserved.Add(tmp.Reserve());
							}
							else
							{
								parameter = method.GetTmpVariable(parameter.UseType(parameters[index].ParameterType)).Reserve();
								reserved.Add((ITmpVariable)parameter);
							}
						}
						arguments.Add(parameter);
					}
					index++;
				}
			}
			else method.PopStack();

			var locals = methodInfo.GetMethodBody().LocalVariables;
			var localVariables = CollectionCache.GetList<IVariable>();
			for(var i = 0; i < locals.Count; i++)
			{
				localVariables.Add(method.GetTmpVariable(locals[i].LocalType).Reserve());
			}

			var outVariable = methodInfo.ReturnType == typeof(void) ? null : method.GetTmpVariable(methodInfo.ReturnType).Reserve();
			var returnAddress = new EmbedAddressLabel();

			bodyBuilder.Build(methodInfo, arguments, localVariables, outVariable, returnAddress, method);

			method.machine.ApplyLabel(returnAddress);

			if(outVariable != null)
			{
				method.machine.AddCopy(outVariable, method.GetOrPushOutVariable(methodInfo.ReturnType, 1), methodInfo.ReturnType);
				outVariable.Release();
			}

			CollectionCache.Release(arguments);

			for(int i = 0; i < reserved.Count; i++)
			{
				reserved[i].Release();
			}
			CollectionCache.Release(reserved);

			for(int i = 0; i < localVariables.Count; i++)
			{
				((ITmpVariable)localVariables[i]).Release();
			}
			CollectionCache.Release(localVariables);
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new InlineLocalCall(modules.GetModule<MethodBodyBuilder>());
			container.RegisterOpBuilder(OpCodes.Call, builder);
		}
	}
}