using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.Helpers;

namespace Katsudon.Builder.Extensions.UdonExtensions
{
	[OperationBuilder]
	public class CallGenericExtern : IOperationBuider
	{
		public int order => 19;

		private CallExtern externCall;

		private CallGenericExtern(CallExtern externCall)
		{
			this.externCall = externCall;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = (MethodInfo)method.currentOp.argument;
			if(!methodInfo.IsGenericMethod) return false;

			var methodDefinition = methodInfo.GetGenericMethodDefinition();
			var parameters = methodInfo.GetParameters();
			if(UdonCacheHelper.cache.TryFindUdonMethod(methodDefinition.IsStatic ? null : method.PeekStack(parameters.Length).type,
				methodDefinition, out var methodId, out var fullName))
			{
				bool hasCompositeTypes = false;
				var definitionParameters = methodDefinition.GetParameters();
				if(methodDefinition.ReturnType.ContainsGenericParameters) hasCompositeTypes = !methodDefinition.ReturnType.IsGenericParameter;
				if(!hasCompositeTypes)
				{
					for(int i = 0; i < definitionParameters.Length; i++)
					{
						if(definitionParameters[i].ParameterType.ContainsGenericParameters && !definitionParameters[i].ParameterType.IsGenericParameter)
						{
							hasCompositeTypes = true;
							break;
						}
					}
				}
				if(hasCompositeTypes)
				{
					var args = methodInfo.GetGenericArguments();
					for(int i = 0; i < args.Length; i++)
					{
						if(args[i] != typeof(object))//FIX: use generic type constraints?
						{
							var methods = methodInfo.DeclaringType.GetMethods((methodInfo.IsPublic ? BindingFlags.Public : BindingFlags.NonPublic) | (methodInfo.IsStatic ? BindingFlags.Static : BindingFlags.Instance));
							var name = methodInfo.Name;
							for(int j = 0; j < methods.Length; j++)
							{
								if(methods[j].Name == name && !methods[j].IsGenericMethodDefinition)
								{
									var methodParams = methods[j].GetParameters();
									if(methodParams.Length == parameters.Length)
									{
										bool isSuitable = true;
										for(int k = 0; k < methodParams.Length; k++)
										{
											if(!methodParams[i].ParameterType.IsAssignableFrom(parameters[i].ParameterType))
											{
												isSuitable = false;
												break;
											}
										}
										if(isSuitable)
										{
											if(externCall.TryCallMethod(method, methods[j]))
											{
												return true;
											}
										}
									}
								}
							}
							throw new Exception("Only the type 'object' can be used as an argument for this generic method: " + methodDefinition);
						}
					}
				}

				var arguments = CollectionCache.GetList<VariableMeta>();
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
							arguments.Add(iterator.Current.UseType(parameter).Mode(parameter.IsByRef ? (VariableMeta.UsageMode.In | VariableMeta.UsageMode.Out) : VariableMeta.UsageMode.None));
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
				CollectionCache.Release(arguments);
				return true;
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new CallGenericExtern(modules.GetModule<CallExtern>());
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
		}
	}
}