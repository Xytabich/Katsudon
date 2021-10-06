using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.Methods;
using Katsudon.Info;

namespace Katsudon.Builder.Extensions.Struct
{
	[OperationBuilder]
	public class StructCall : IOperationBuider
	{
		public int order => 4;

		private AssembliesInfo assemblies;
		private MethodBodyBuilder bodyBuilder;

		public StructCall(AssembliesInfo assemblies, MethodBodyBuilder bodyBuilder)
		{
			this.assemblies = assemblies;
			this.bodyBuilder = bodyBuilder;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodBase = (MethodBase)method.currentOp.argument;
			if(methodBase.IsAbstract || methodBase.IsStatic) return false;
			if(!Utils.IsUdonAsmStruct(methodBase.DeclaringType)) return false;

			var parameters = methodBase.GetParameters();

			IVariable selfVariable = null;
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
					if(index < 0)
					{
						selfVariable = iterator.Current;
					}
					else
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
			else
			{
				selfVariable = method.PopStack();
			}

			var locals = methodBase.GetMethodBody().LocalVariables;
			var localVariables = CollectionCache.GetList<IVariable>();
			for(var i = 0; i < locals.Count; i++)
			{
				localVariables.Add(method.GetTmpVariable(locals[i].LocalType).Reserve());
			}

			selfVariable = method.GetTmpVariable(selfVariable.OwnType()).Reserve();

			ITmpVariable outVariable = null;
			if(methodBase is MethodInfo methodInfo && methodInfo.ReturnType != typeof(void))
			{
				outVariable = method.GetTmpVariable(methodInfo.ReturnType);
				outVariable.Allocate();
				outVariable.Reserve();
			}

			var returnAddress = new EmbedAddressLabel();
			bodyBuilder.Build(methodBase, new StructMethodDescriptor(selfVariable, arguments, localVariables, outVariable, returnAddress), method);
			method.machine.ApplyLabel(returnAddress);

			if(outVariable != null)
			{
				outVariable.Release();
				method.PushStack(outVariable);
			}
			((ITmpVariable)selfVariable).Release();

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
			var builder = new StructCall(modules.GetModule<AssembliesInfo>(), modules.GetModule<MethodBodyBuilder>());
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
		}

		internal class StructMethodDescriptor : MethodDescriptor
		{
			private IVariable selfVariable;

			public StructMethodDescriptor(IVariable selfVariable, IList<IVariable> arguments,
				IList<IVariable> locals, IVariable returnVariable, IAddressLabel returnAddress) :
				base(false, false, arguments, locals, returnVariable, returnAddress)
			{
				this.selfVariable = selfVariable;
			}

			protected override IUdonMachine CreateMachine(IUdonProgramBlock block)
			{
				return new ThisReplacer(selfVariable, (IRawUdonMachine)block.machine, this);
			}

			private class ThisReplacer : MethodOpTracker, IRawUdonMachine
			{
				private IVariable selfVariable;

				public ThisReplacer(IVariable selfVariable, IRawUdonMachine machine, MethodDescriptor method) : base(machine, method)
				{
					this.selfVariable = selfVariable;
				}

				public override IVariable GetThisVariable(UdonThisType type = UdonThisType.Self)
				{
					if(type != UdonThisType.Self) throw new Exception("Struct can only use itself as this target");
					return selfVariable;
				}
			}
		}
	}
}