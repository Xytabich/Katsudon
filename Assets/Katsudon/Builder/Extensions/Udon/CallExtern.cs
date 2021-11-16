using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.Helpers;

namespace Katsudon.Builder.Extensions.UdonExtensions
{
	[OperationBuilder]
	public class CallExtern : IOperationBuider
	{
		public int order => 20;

		private List<VariableMeta> arguments = new List<VariableMeta>();

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			return TryCallMethod(method, (MethodInfo)method.currentOp.argument);
		}

		public bool TryCallMethod(IMethodDescriptor method, MethodInfo methodInfo)
		{
			var parameters = methodInfo.GetParameters();
			if(UdonCacheHelper.cache.TryFindUdonMethod(methodInfo.IsStatic ? null : method.PeekStack(parameters.Length).type, methodInfo, out var methodId, out var fullName))
			{
				int popCount = 0;
				if(!methodInfo.IsStatic) popCount++;
				popCount += parameters.Length;
				if(popCount > 0)
				{
					var iterator = method.PopMultiple(popCount);
					int index = methodInfo.IsStatic ? 0 : -1;
					while(iterator.MoveNext())
					{
						if(index >= 0)
						{
							var parameter = parameters[index].ParameterType;
							var mode = VariableMeta.UsageMode.In;
							if(parameter.IsByRef)
							{
								if(parameters[index].IsOut)
								{
									mode = VariableMeta.UsageMode.OutOnly | VariableMeta.UsageMode.Out;
								}
								else if(!parameters[index].IsIn)
								{
									mode |= VariableMeta.UsageMode.Out;
								}
							}
							arguments.Add(iterator.Current.UseType(parameter).Mode(mode));
						}
						else
						{
							arguments.Add(iterator.Current.UseType(methodInfo.DeclaringType).Mode(VariableMeta.UsageMode.In | VariableMeta.UsageMode.Out));
						}
						index++;
					}
				}

				if(methodInfo.ReturnType == typeof(void))
				{
					method.machine.AddExtern(fullName, arguments.ToArray());
				}
				else
				{
					method.machine.AddExtern(fullName, () => method.GetOrPushOutVariable(methodInfo.ReturnType), arguments.ToArray());
				}
				arguments.Clear();
				return true;
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new CallExtern();
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
			modules.AddModule(builder);
		}
	}
}