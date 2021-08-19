using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Katsudon.Builder.Extensions.Inlining
{
	[OperationBuilder]
	public class InlineStaticMethod : IOperationBuider
	{
		int IOperationBuider.order => 15;

		private MethodBodyBuilder bodyBuilder;

		private List<IVariable> argumentsCache = new List<IVariable>();//TODO: cache
		private List<ITmpVariable> reservedCache = new List<ITmpVariable>();
		private List<IVariable> localsCache = new List<IVariable>();

		public InlineStaticMethod(MethodBodyBuilder bodyBuilder)
		{
			this.bodyBuilder = bodyBuilder;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = (MethodInfo)method.currentOp.argument;
			if(!methodInfo.IsStatic || !Utils.IsUdonAsm(methodInfo.DeclaringType)) return false;

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
						parameter = method.GetTmpVariable(parameter).Reserve();
						reservedCache.Add((ITmpVariable)parameter);
					}
					argumentsCache.Add(parameter);
					index++;
				}
			}

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
			var builder = new InlineStaticMethod(modules.GetModule<MethodBodyBuilder>());
			container.RegisterOpBuilder(OpCodes.Call, builder);
		}
	}
}