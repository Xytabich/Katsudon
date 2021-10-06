using System.ComponentModel;
using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Info;

namespace Katsudon.Builder.Extensions.Struct
{
	[OperationBuilder]
	public class StructCtor : IOperationBuider
	{
		public int order => 0;

		private AssembliesInfo assemblies;
		private MethodBodyBuilder bodyBuilder;

		public StructCtor(AssembliesInfo assemblies, MethodBodyBuilder bodyBuilder)
		{
			this.assemblies = assemblies;
			this.bodyBuilder = bodyBuilder;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var ctorInfo = (ConstructorInfo)method.currentOp.argument;
			if(ctorInfo.IsStatic) return false;
			if(!Utils.IsUdonAsmStruct(ctorInfo.DeclaringType)) return false;

			var parameters = ctorInfo.GetParameters();
			var reserved = CollectionCache.GetList<ITmpVariable>();
			var arguments = CollectionCache.GetList<IVariable>();
			int argsCount = parameters.Length;
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

			var locals = ctorInfo.GetMethodBody().LocalVariables;
			var localVariables = CollectionCache.GetList<IVariable>();
			for(var i = 0; i < locals.Count; i++)
			{
				localVariables.Add(method.GetTmpVariable(locals[i].LocalType).Reserve());
			}

			var selfVariable = method.GetTmpVariable(ctorInfo.DeclaringType);
			selfVariable.Allocate();
			selfVariable.Reserve();

			method.machine.AddExtern("SystemArray.__Clone__SystemObject", selfVariable,
				method.machine.GetConstVariable(new StructPrototype(ctorInfo.DeclaringType)).OwnType());

			var returnAddress = new EmbedAddressLabel();
			bodyBuilder.Build(ctorInfo, new StructCall.StructMethodDescriptor(selfVariable, arguments, localVariables, null, returnAddress), method);
			method.machine.ApplyLabel(returnAddress);

			method.PushStack(selfVariable);
			selfVariable.Release();

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
			var builder = new StructCtor(modules.GetModule<AssembliesInfo>(), modules.GetModule<MethodBodyBuilder>());
			container.RegisterOpBuilder(OpCodes.Newobj, builder);
		}
	}
}