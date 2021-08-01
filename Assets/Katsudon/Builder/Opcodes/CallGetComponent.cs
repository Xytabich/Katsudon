using System;
using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;
using Katsudon.Info;
using Katsudon.Utility;
using UnityEngine;
using VRC.Udon;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class CallGetComponent : IOperationBuider
	{
		private const string CALL_GENERIC_FORMAT = "UnityEngineComponent.__{0}__T";
		private const string CALL_GENERIC_INCLUDING_FORMAT = "UnityEngineComponent.__{0}__SystemBoolean__T";

		private const string CALL_METHOD_FORMAT = "UnityEngineComponent.__{0}__SystemType__UnityEngineComponent";
		private const string CALL_METHOD_INCLUDING_FORMAT = "UnityEngineComponent.__{0}__SystemType_SystemBoolean__UnityEngineComponent";

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
			if((getterName == "GetComponent" || getterName == "GetComponentInParent" || getterName == "GetComponentInChildren") &&
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
				IVariable typeVariable;

				if(methodInfo.IsGenericMethod)
				{
					var searchType = methodInfo.GetGenericArguments()[0];
					if(Utils.IsUdonAsm(searchType))
					{
						var targetVariable = method.PopStack();
						BuildGetUdonComponent(method, targetVariable, getterName,
							method.machine.GetConstVariable(assemblies.GetTypeInfo(searchType).guid),
							includeInactive, method.GetOrPushOutVariable(searchType)
						);
						return true;
					}
					else if(searchType != typeof(UdonBehaviour))
					{
						var targetVariable = method.PopStack();
						ExternCall(method,
							string.Format(includeInactive == null ? CALL_GENERIC_FORMAT : CALL_GENERIC_INCLUDING_FORMAT, getterName),
							targetVariable, method.machine.GetConstVariable(searchType, typeof(Type)),
							true, includeInactive,
							method.GetOrPushOutVariable(searchType)
						);
						return true;
					}
					else
					{
						typeVariable = method.machine.GetConstVariable(searchType, typeof(Type));
					}
				}
				else
				{
					typeVariable = method.PopStack();
				}

				{
					var targetVariable = method.PopStack();

					if(typeVariable is IConstVariable constVariable)//TODO: move to GetComponentT part
					{
						if(Utils.IsUdonAsm((Type)constVariable.value))
						{
							BuildGetUdonComponent(method, targetVariable, getterName, typeVariable, includeInactive, method.GetOrPushOutVariable(typeof(Component)));
							return true;
						}
						else
						{
							ExternCall(method,
								string.Format(includeInactive == null ? CALL_METHOD_FORMAT : CALL_METHOD_INCLUDING_FORMAT, getterName),
								targetVariable, typeVariable,
								false, includeInactive,
								method.GetOrPushOutVariable(typeof(Component))
							);
							return true;
						}
					}
					else
					{
						var outVariable = method.GetOrPushOutVariable(typeof(Component));
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
						method.machine.BinaryOperatorExtern(BinaryOperator.Inequality, typeOfType, method.machine.GetConstVariable(typeof(Guid), typeof(Type)), guidCondition);
						var guidSearchLabel = new EmbedAddressLabel();
						method.machine.AddBranch(guidCondition, guidSearchLabel);

						typeVariable.Allocate();
						ExternCall(method,
							string.Format(includeInactive == null ? CALL_METHOD_FORMAT : CALL_METHOD_INCLUDING_FORMAT, getterName),
							targetVariable, typeVariable,
							false, includeInactive,
							outVariable
						);
						var endLabel = new EmbedAddressLabel();
						method.machine.AddJump(endLabel);

						method.machine.ApplyLabel(guidSearchLabel);
						BuildGetUdonComponent(method, targetVariable, getterName, typeVariable, includeInactive, outVariable);

						method.machine.ApplyLabel(endLabel);
						return true;
					}
				}
			}
			return false;
		}

		private static void BuildGetUdonComponent(IMethodDescriptor method, IVariable targetVariable, string getterName, IVariable searchType, IVariable includeInactive, IVariable componentVariable)
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
				case "GetComponent": getterName = "GetComponents"; break;
				case "GetComponentInParent": getterName = "GetComponentsInParent"; break;
				case "GetComponentInChildren": getterName = "GetComponentsInChildren"; break;
			}

			var components = method.GetTmpVariable(typeof(Component[])).Reserve();

			ExternCall(method,
				string.Format(includeInactive == null ? CallGetComponents.CALL_METHOD_FORMAT : CallGetComponents.CALL_METHOD_INCLUDING_FORMAT, getterName),
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