using System;
using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;
using Katsudon.Info;
using Katsudon.Utility;
using UnityEngine;
using VRC.Udon;

namespace Katsudon.Builder.Extensions.UnityExtensions
{
	[OperationBuilder]
	public class CallGetComponent : IOperationBuider
	{
		public int order => 15;

		private AssembliesInfo assemblies;

		private CallGetComponent(AssembliesInfo assemblies)
		{
			this.assemblies = assemblies;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = method.currentOp.argument as MethodInfo;
			var getterName = methodInfo.Name;
			if((getterName == nameof(Component.GetComponent) ||
				getterName == nameof(Component.GetComponentInParent) ||
				getterName == nameof(Component.GetComponentInChildren)) &&
				(methodInfo.DeclaringType == typeof(Component) || methodInfo.DeclaringType == typeof(GameObject)))
			{
				var parameters = methodInfo.GetParameters();
				if(!methodInfo.IsGenericMethod && parameters[0].ParameterType != typeof(Type))
				{
					return false;
				}

				IVariable includeInactive = null;
				if(parameters.Length == (methodInfo.IsGenericMethod ? 1 : 2))
				{
					if(parameters[parameters.Length - 1].ParameterType != typeof(bool))
					{
						return false;
					}
					includeInactive = method.PopStack();
				}
				IVariable typeVariable = null;
				if(!methodInfo.IsGenericMethod)
				{
					typeVariable = method.PopStack();
				}
				var targetVariable = method.PopStack();
				BuildGetComponent(method, assemblies, methodInfo, getterName, targetVariable,
				   typeVariable, includeInactive, (type) => method.GetOrPushOutVariable(type));
				return true;
			}
			return false;
		}

		public static void BuildGetComponent(IMethodDescriptor method, AssembliesInfo assemblies, MethodInfo methodInfo,
			string getterName, IVariable targetVariable, IVariable typeVariable, IVariable includeInactive, Func<Type, IVariable> outCtor)
		{
			bool isGameObject = methodInfo.DeclaringType == typeof(GameObject);
			if(methodInfo.IsGenericMethod)
			{
				var searchType = methodInfo.GetGenericArguments()[0];
				if(Utils.IsUdonAsm(searchType))
				{
					BuildGetUdonComponent(method, targetVariable, isGameObject, getterName,
						method.machine.GetConstVariable(assemblies.GetTypeInfo(searchType).guid),
						includeInactive, outCtor(searchType)
					);
					return;
				}
				else if(searchType != typeof(UdonBehaviour))
				{
					ExternCall(method,
						GetGenericExternName(isGameObject, includeInactive == null, getterName),
						targetVariable, method.machine.GetConstVariable(searchType, typeof(Type)),
						true, includeInactive,
						outCtor(searchType)
					);
					return;
				}
				else
				{
					typeVariable = method.machine.GetConstVariable(searchType, typeof(Type));
				}
			}

			if(typeVariable is IConstVariable constVariable)//TODO: move to GetComponentT part
			{
				if(Utils.IsUdonAsm((Type)constVariable.value))
				{
					BuildGetUdonComponent(method, targetVariable, isGameObject, getterName,
						typeVariable, includeInactive, outCtor(typeof(Component)));
				}
				else
				{
					ExternCall(method,
						GetExternName(isGameObject, includeInactive == null, getterName),
						targetVariable, typeVariable,
						false, includeInactive,
						outCtor(typeof(Component))
					);
				}
			}
			else
			{
				var outVariable = outCtor(typeof(Component));
				/*
				Component GetComponent(Type|Guid searchType)
				{
					if(searchType.GetType() != typeof(Guid)) return GetComponent(searchType);

					// ... search by guid
				}
				*/
				var typeOfType = method.GetTmpVariable(typeof(Type));
				typeVariable.Allocate();
				method.machine.AddExtern("SystemObject.__GetType__SystemType", typeOfType, typeVariable.OwnType());
				var guidCondition = method.GetTmpVariable(typeof(bool));
				method.machine.BinaryOperatorExtern(BinaryOperator.Inequality, typeOfType,
					method.machine.GetConstVariable(typeof(Guid), typeof(Type)), guidCondition);
				var guidSearchLabel = new EmbedAddressLabel();
				method.machine.AddBranch(guidCondition, guidSearchLabel);

				typeVariable.Allocate();
				ExternCall(method,
					GetExternName(isGameObject, includeInactive == null, getterName),
					targetVariable, typeVariable,
					false, includeInactive,
					outVariable
				);
				var endLabel = new EmbedAddressLabel();
				method.machine.AddJump(endLabel);

				method.machine.ApplyLabel(guidSearchLabel);
				BuildGetUdonComponent(method, targetVariable, isGameObject, getterName, typeVariable, includeInactive, outVariable);

				method.machine.ApplyLabel(endLabel);
			}
		}

		private static string GetExternName(bool gameObject, bool defaultCall, string getterName)
		{
			const string CALL_METHOD_FORMAT = "{0}.__{1}__SystemType__UnityEngineComponent";
			const string CALL_METHOD_INCLUDING_FORMAT = "{0}.__{1}__SystemType_SystemBoolean__UnityEngineComponent";
			return string.Format(defaultCall ? CALL_METHOD_FORMAT : CALL_METHOD_INCLUDING_FORMAT,
				gameObject ? "UnityEngineGameObject" : "UnityEngineComponent", getterName);
		}

		private static string GetGenericExternName(bool gameObject, bool defaultCall, string getterName)
		{
			const string CALL_METHOD_FORMAT = "{0}.__{1}__T";
			const string CALL_METHOD_INCLUDING_FORMAT = "{0}.__{1}__SystemBoolean__T";
			return string.Format(defaultCall ? CALL_METHOD_FORMAT : CALL_METHOD_INCLUDING_FORMAT,
				gameObject ? "UnityEngineGameObject" : "UnityEngineComponent", getterName);
		}

		private static void BuildGetUdonComponent(IMethodDescriptor method, IVariable targetVariable,
			bool isGameObject, string getterName, IVariable searchType, IVariable includeInactive, IVariable componentVariable)
		{
			//FIX: small search optimization - if the type is interface or abstract, the guid comparison step can be skipped
			/*
			Component GetUdonComponent(Guid searchTypeId)
			{
				var components = GetComponents(typeof(UdonBehaviour));
				for(int i = 0; i < components.Length; i++)
				{
					var element = components[i];
					if(element.typeId == searchTypeId) return element;
					if(Array.BinarySearch(element.inherits, searchTypeId) >= 0) return element;
				}
				return null;
			}
			*/
			switch(getterName)
			{
				case nameof(Component.GetComponent): getterName = nameof(Component.GetComponents); break;
				case nameof(Component.GetComponentInParent): getterName = nameof(Component.GetComponentsInParent); break;
				case nameof(Component.GetComponentInChildren): getterName = nameof(Component.GetComponentsInChildren); break;
			}

			var components = method.GetTmpVariable(typeof(Component[])).Reserve();

			ExternCall(method,
				CallGetComponents.GetExternName(isGameObject, includeInactive == null, getterName),
				targetVariable, method.machine.GetConstVariable(typeof(UdonBehaviour), typeof(Type)),
				false, includeInactive,
				components
			);

			var endSuccessLabel = new EmbedAddressLabel();
			using(ForLoop.Array(method, components, out var componentsIndex))
			{
				method.machine.AddExtern("UnityEngineComponentArray.__Get__SystemInt32__UnityEngineComponent",
					componentVariable, components.OwnType(), componentsIndex.OwnType());

				// if(element.typeId == searchTypeId) return element;
				var componentGuid = method.GetTmpVariable(typeof(Guid));
				componentVariable.Allocate();
				method.machine.GetVariableExtern(componentVariable, AsmTypeInfo.TYPE_ID_NAME, componentGuid);

				searchType.Allocate();
				var condition = method.GetTmpVariable(typeof(bool));
				method.machine.AddExtern(
					BinaryOperatorExtension.GetExternName(BinaryOperator.Inequality, typeof(Guid), typeof(Guid), typeof(bool)),
					condition, componentGuid.OwnType(), searchType.OwnType()
				);
				method.machine.AddBranch(condition, endSuccessLabel);
				// endif

				// if(Array.BinarySearch(element.inherits, searchTypeId) >= 0) return element;
				var inherits = method.GetTmpVariable(typeof(Guid[]));
				componentVariable.Allocate();
				method.machine.GetVariableExtern(componentVariable, AsmTypeInfo.INHERIT_IDS_NAME, inherits);

				var inheritsIndex = method.GetTmpVariable(typeof(int));
				method.machine.AddExtern("SystemArray.__BinarySearch__SystemArray_SystemObject__SystemInt32",
					inheritsIndex, inherits.OwnType(), searchType.OwnType());

				condition = method.GetTmpVariable(typeof(bool));
				method.machine.BinaryOperatorExtern(BinaryOperator.LessThan, inheritsIndex, method.machine.GetConstVariable((int)0), condition);
				method.machine.AddBranch(condition, endSuccessLabel);
				// endif
			}
			componentVariable.Allocate();
			method.machine.AddCopy(method.machine.GetConstVariable(null), componentVariable);

			method.machine.ApplyLabel(endSuccessLabel);

			components.Release();
		}

		private static void ExternCall(IMethodDescriptor method, string name, IVariable targetVariable,
			IVariable typeVariable, bool isGeneric, IVariable includeInactive, IVariable outVariable)
		{
			if(includeInactive == null)
			{
				method.machine.AddExtern(name, outVariable, targetVariable.OwnType(), typeVariable.OwnType());
			}
			else
			{
				var variables = new VariableMeta[3];
				variables[0] = targetVariable.OwnType();
				variables[isGeneric ? 2 : 1] = typeVariable.OwnType();
				variables[isGeneric ? 1 : 2] = includeInactive.UseType(typeof(bool));
				method.machine.AddExtern(name, outVariable, variables);
			}
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new CallGetComponent(modules.GetModule<AssembliesInfo>());
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
		}
	}
}