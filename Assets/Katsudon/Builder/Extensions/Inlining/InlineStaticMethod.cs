using System.ComponentModel;
using System.Reflection;
using System.Reflection.Emit;

namespace Katsudon.Builder.Extensions.Inlining
{
	[OperationBuilder]
	public class InlineStaticMethod : IOperationBuider
	{
		int IOperationBuider.order => 15;

		private MethodBodyBuilder bodyBuilder;

		public InlineStaticMethod(MethodBodyBuilder bodyBuilder)
		{
			this.bodyBuilder = bodyBuilder;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = (MethodInfo)method.currentOp.argument;
			if(!methodInfo.IsStatic || !Utils.IsUdonAsm(methodInfo.DeclaringType)) return false;

			var parameters = methodInfo.GetParameters();

			var reserved = CollectionCache.GetList<ITmpVariable>();
			var arguments = CollectionCache.GetList<IVariable>();
			int argsCount = parameters.Length;
			if(argsCount != 0)
			{
				var iterator = method.PopMultiple(argsCount);
				ReadOnlyAttribute readOnly;
				int index = 0;
				while(iterator.MoveNext())
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
					index++;
				}
			}

			var locals = methodInfo.GetMethodBody().LocalVariables;
			var localsCache = CollectionCache.GetList<IVariable>();
			for(var i = 0; i < locals.Count; i++)
			{
				localsCache.Add(method.GetTmpVariable(locals[i].LocalType).Reserve());
			}

			ITmpVariable outVariable = null;
			if(methodInfo.ReturnType != typeof(void))
			{
				outVariable = method.GetTmpVariable(methodInfo.ReturnType);
				outVariable.Allocate();
				outVariable.Reserve();
			}

			var returnAddress = new EmbedAddressLabel();
			bodyBuilder.Build(methodInfo, false, arguments, localsCache, outVariable, returnAddress, method);
			method.machine.ApplyLabel(returnAddress);

			if(outVariable != null)
			{
				outVariable.Release();
				method.PushStack(outVariable);
			}

			CollectionCache.Release(arguments);

			for(int i = 0; i < reserved.Count; i++)
			{
				reserved[i].Release();
			}
			CollectionCache.Release(reserved);

			for(int i = 0; i < localsCache.Count; i++)
			{
				((ITmpVariable)localsCache[i]).Release();
			}
			CollectionCache.Release(localsCache);
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new InlineStaticMethod(modules.GetModule<MethodBodyBuilder>());
			container.RegisterOpBuilder(OpCodes.Call, builder);
		}
	}
}