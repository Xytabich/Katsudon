using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class InlineLocalCall : IOperationBuider
	{
		public int order => 80;

		private MethodBodyBuilder bodyBuilder;

		private List<IVariable> argumentsCache = new List<IVariable>();//TODO: cache
		private List<ITmpVariable> reservedCache = new List<ITmpVariable>();
		private List<IVariable> localsCache = new List<IVariable>();

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

			var target = method.PeekStack(methodInfo.GetParameters().Length);
			if(!(target is ThisVariable))
			{
				return false;
			}

			var parameters = methodInfo.GetParameters();

			int argsCount = parameters.Length;
			if(argsCount != 0)
			{
				var iterator = method.PopMultiple(argsCount);
				int index = 0;
				while(iterator.MoveNext())
				{
					var parameter = iterator.Current;
					if(!parameters[index].ParameterType.IsByRef)
					{
						parameter = method.GetReadonlyVariable(parameter.UseType(parameters[index].ParameterType));
						if(parameter is ITmpVariable tmpVariable)
						{
							tmpVariable.Reserve();
							reservedCache.Add(tmpVariable);
						}
					}
					argumentsCache.Add(parameter);
					index++;
				}
			}
			method.PopStack();

			var locals = methodInfo.GetMethodBody().LocalVariables;
			for(var i = 0; i < locals.Count; i++)
			{
				localsCache.Add(method.GetTmpVariable(locals[i].LocalType).Reserve());
			}

			var outVariable = methodInfo.ReturnType == typeof(void) ? null : method.GetTmpVariable(methodInfo.ReturnType).Reserve();
			var returnAddress = new EmbedAddressLabel();

			bodyBuilder.Build(methodInfo, argumentsCache, localsCache, outVariable, returnAddress, method);

			method.machine.ApplyLabel(returnAddress);

			if(outVariable != null)
			{
				method.machine.AddCopy(outVariable, method.GetOrPushOutVariable(methodInfo.ReturnType, 1), methodInfo.ReturnType);
				outVariable.Release();
			}

			argumentsCache.Clear();
			for(int i = 0; i < reservedCache.Count; i++)
			{
				reservedCache[i].Release();
			}
			reservedCache.Clear();

			for(int i = 0; i < localsCache.Count; i++)
			{
				((ITmpVariable)localsCache[i]).Release();
			}
			localsCache.Clear();
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new InlineLocalCall(modules.GetModule<MethodBodyBuilder>());
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
		}
	}
}