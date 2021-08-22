using System;
using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;
using Katsudon.Builder.Variables;
using Katsudon.Info;
using Katsudon.Utility;
using UnityEngine;
using VRC.Udon;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class CallGetComponents : IOperationBuider
	{
		public int order => 15;

		private AssembliesInfo assemblies;

		private CallGetComponents(AssembliesInfo assemblies)
		{
			this.assemblies = assemblies;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = method.currentOp.argument as MethodInfo;
			var getterName = methodInfo.Name;
			if((getterName == nameof(Component.GetComponents) ||
				getterName == nameof(Component.GetComponentsInParent) ||
				getterName == nameof(Component.GetComponentsInChildren)) &&
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

				bool isGameObject = methodInfo.DeclaringType == typeof(GameObject);

				if(methodInfo.IsGenericMethod)
				{
					var searchType = methodInfo.GetGenericArguments()[0];
					if(Utils.IsUdonAsm(searchType))
					{
						var targetVariable = method.PopStack();
						BuildGetUdonComponents(method, targetVariable, isGameObject, getterName,
							method.machine.GetConstVariable(assemblies.GetTypeInfo(searchType).guid),
							includeInactive,
							method.GetOrPushOutVariable(searchType.MakeArrayType())
						);
						return true;
					}
					else if(searchType != typeof(UdonBehaviour))
					{
						var targetVariable = method.PopStack();
						ExternCall(method,
							GetGenericExternName(isGameObject, includeInactive == null, getterName),
							targetVariable, method.machine.GetConstVariable(searchType, typeof(Type)),
							true, includeInactive,
							method.GetOrPushOutVariable(searchType.MakeArrayType())
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

					if(typeVariable is IConstVariable constVariable)
					{
						if(Utils.IsUdonAsm((Type)constVariable.value))
						{
							BuildGetUdonComponents(method, targetVariable, isGameObject, getterName,
								typeVariable, includeInactive, method.GetOrPushOutVariable(typeof(Component[])));
							return true;
						}
						else
						{
							ExternCall(method,
								GetExternName(isGameObject, includeInactive == null, getterName),
								targetVariable, typeVariable,
								false, includeInactive,
								method.GetOrPushOutVariable(typeof(Component[]))
							);
							return true;
						}
					}
					else
					{
						var outVariable = method.GetOrPushOutVariable(typeof(Component[]));
						/*
						Component[] GetComponents(Type|Guid searchType)
						{
							if(searchType.GetType() != typeof(Guid)) return GetComponents(searchType);

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
							GetExternName(isGameObject, includeInactive == null, getterName),
							targetVariable, typeVariable,
							false, includeInactive,
							outVariable
						);
						var endLabel = new EmbedAddressLabel();
						method.machine.AddJump(endLabel);

						method.machine.ApplyLabel(guidSearchLabel);
						BuildGetUdonComponents(method, targetVariable, isGameObject, getterName, typeVariable, includeInactive, outVariable);

						method.machine.ApplyLabel(endLabel);
						return true;
					}
				}
			}
			return false;
		}

		public static string GetExternName(bool gameObject, bool includeInactive, string getterName)
		{
			const string CALL_METHOD_FORMAT = "{0}.__{1}__SystemType__UnityEngineComponentArray";
			const string CALL_METHOD_INCLUDING_FORMAT = "{0}.__{1}__SystemType_SystemBoolean__UnityEngineComponentArray";
			return string.Format(includeInactive ? CALL_METHOD_FORMAT : CALL_METHOD_INCLUDING_FORMAT,
				gameObject ? "UnityEngineGameObject" : "UnityEngineComponent", getterName);
		}

		private static string GetGenericExternName(bool gameObject, bool includeInactive, string getterName)
		{
			const string CALL_METHOD_FORMAT = "{0}.__{1}__TArray";
			const string CALL_METHOD_INCLUDING_FORMAT = "{0}.__{1}__SystemBoolean__TArray";
			return string.Format(includeInactive ? CALL_METHOD_FORMAT : CALL_METHOD_INCLUDING_FORMAT,
				gameObject ? "UnityEngineGameObject" : "UnityEngineComponent", getterName);
		}

		private static void BuildGetUdonComponents(IMethodDescriptor method, IVariable targetVariable,
			bool isGameObject, string getterName, IVariable searchType, IVariable includeInactive, IVariable outComponents)
		{
			/*
			Component[] GetUdonComponents(Guid searchTypeId)
			{
				int counter = 0;
				var components = GetComponents(typeof(UdonBehaviour));
				for(int i = 0; i < components.Length; i++)
				{
					var element = components[i];
					if(element.typeId == searchTypeId || Array.BinarySearch(element.inherits, searchTypeId) >= 0)
					{
						components[counter] = element;
						counter++;
					}
				}
				var outComponents = new Component[counter];
				Array.Copy(components, outComponents, counter);
				return outComponents;
			}
			*/

			var counter = method.GetTmpVariable(typeof(int)).Reserve();
			var components = method.GetTmpVariable(typeof(Component[])).Reserve();
			var component = method.GetTmpVariable(typeof(Component)).Reserve();

			ExternCall(method,
				GetExternName(isGameObject, includeInactive == null, getterName),
				targetVariable, method.machine.GetConstVariable(typeof(UdonBehaviour), typeof(Type)),
				false, includeInactive,
				components
			);

			method.machine.AddCopy(method.machine.GetConstVariable((int)0), counter);

			using(ForLoop.Array(method, components, out var componentsIndex))
			{
				method.machine.AddExtern("UnityEngineComponentArray.__Get__SystemInt32__UnityEngineComponent",
					component, components.OwnType(), componentsIndex.OwnType());

				var addToListLabel = new EmbedAddressLabel();

				// if(element.typeId == searchTypeId)
				var componentGuid = method.GetTmpVariable(typeof(Guid));
				method.machine.GetVariableExtern(component, AsmTypeInfo.TYPE_ID_NAME, componentGuid);

				searchType.Allocate();
				var condition = method.GetTmpVariable(typeof(bool));
				method.machine.AddExtern(
					BinaryOperatorExtension.GetExternName(BinaryOperator.Inequality, typeof(Guid), typeof(Guid), typeof(bool)),
					condition, componentGuid.OwnType(), searchType.OwnType()
				);
				method.machine.AddBranch(condition, addToListLabel);
				// endif

				// if(Array.BinarySearch(element.inherits, searchTypeId) >= 0)
				var inherits = method.GetTmpVariable(typeof(Guid[]));
				method.machine.GetVariableExtern(component, AsmTypeInfo.INHERIT_IDS_NAME, inherits);

				var inheritsIndex = method.GetTmpVariable(typeof(int));
				method.machine.AddExtern("SystemArray.__BinarySearch__SystemArray_SystemObject__SystemInt32",
					inheritsIndex, inherits.OwnType(), searchType.OwnType());

				condition = method.GetTmpVariable(typeof(bool));
				method.machine.BinaryOperatorExtern(BinaryOperator.LessThan, inheritsIndex, method.machine.GetConstVariable((int)0), condition);
				method.machine.AddBranch(condition, addToListLabel);
				// endif
				var continueLoopLabel = new EmbedAddressLabel();
				method.machine.AddJump(continueLoopLabel);

				method.machine.ApplyLabel(addToListLabel);
				// components[counter++] = element;
				method.machine.AddExtern("UnityEngineComponentArray.__Set__SystemInt32_UnityEngineComponent__SystemVoid",
					components.OwnType(), counter.OwnType(), component.OwnType());
				method.machine.BinaryOperatorExtern(BinaryOperator.Addition, counter, method.machine.GetConstVariable((int)1), counter);
				// end

				method.machine.ApplyLabel(continueLoopLabel);
			}

			var udonType = ArrayTypes.GetUdonArrayType(outComponents.type);
			method.machine.AddExtern(Utils.GetExternName(udonType, "__ctor__SystemInt32__{0}", udonType), outComponents, counter.OwnType());
			outComponents.Allocate();
			method.machine.AddExtern("SystemArray.__Copy__SystemArray_SystemArray_SystemInt32__SystemVoid",
				components.OwnType(), outComponents.OwnType(), counter.OwnType());

			counter.Release();
			components.Release();
			component.Release();
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
			var builder = new CallGetComponents(modules.GetModule<AssembliesInfo>());
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
		}
	}
}