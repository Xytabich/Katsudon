using System;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class CallInstantiate : IOperationBuider
	{
		public int order => 15;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = method.currentOp.argument as MethodInfo;
			var methodName = methodInfo.Name;
			if(methodName == nameof(UnityEngine.Object.Instantiate) && methodInfo.DeclaringType == typeof(UnityEngine.Object))
			{
				if(methodInfo.IsGenericMethod)
				{
					var type = methodInfo.GetGenericArguments()[0];
					if(type == typeof(GameObject) || typeof(Component).IsAssignableFrom(type))
					{
						var parameters = methodInfo.GetParameters();
						IVariable worldPositionStays = null;
						if(HasParameter(parameters, 2, typeof(bool)))
						{
							worldPositionStays = method.PopStack();
						}
						IVariable parent = null;
						if(HasParameter(parameters, 1, typeof(Transform)) || HasParameter(parameters, 3, typeof(Transform)))
						{
							parent = method.PopStack();
						}
						IVariable position = null, rotation = null;
						if(HasParameter(parameters, 1, typeof(Vector3)))
						{
							rotation = method.PopStack();
							position = method.PopStack();
						}
						IVariable original = method.PopStack();
						if(type == typeof(GameObject) && parameters.Length == 1)
						{
							method.machine.AddExtern("VRCInstantiate.__Instantiate__UnityEngineGameObject__UnityEngineGameObject",
								() => method.GetTmpVariable(typeof(GameObject)),
								original.OwnType()
							);
						}
						else
						{
							var objOriginal = original;
							IVariable componentIndex = null;
							if(type != typeof(GameObject))
							{
								objOriginal = method.GetTmpVariable(typeof(GameObject));
								original.Allocate();
								method.machine.AddExtern("UnityEngineComponent.__get_gameObject__UnityEngineGameObject", objOriginal, original.OwnType());
								var components = method.GetTmpVariable(typeof(Component[]));
								objOriginal.Allocate();
								method.machine.AddExtern("UnityEngineGameObject.__GetComponents__SystemType__UnityEngineComponentArray",
									components, objOriginal.OwnType(), method.machine.GetConstVariable(typeof(Component), typeof(Type)).OwnType());
								componentIndex = method.GetTmpVariable(typeof(int));
								method.machine.AddExtern("SystemArray.__IndexOf__SystemArray_SystemObject__SystemInt32",
									componentIndex, components.OwnType(), original.OwnType());
							}

							bool copyWorldTransform = parent != null && worldPositionStays != null &&
								(!(worldPositionStays is IConstVariable wpsc) || Convert.ToBoolean(wpsc.value));
							IVariable scale = null;
							if(copyWorldTransform)
							{
								ITmpVariable originalTransform = null;
								GetTransform(method, objOriginal, ref originalTransform);
								method.machine.AddExtern("UnityEngineTransform.__get_position__UnityEngineVector3",
									(position = method.GetTmpVariable(typeof(Vector3))), originalTransform.OwnType());
								method.machine.AddExtern("UnityEngineTransform.__get_rotation__UnityEngineQuaternion",
									(rotation = method.GetTmpVariable(typeof(Quaternion))), originalTransform.OwnType());
								method.machine.AddExtern("UnityEngineTransform.__get_lossyScale__UnityEngineVector3",
									(scale = method.GetTmpVariable(typeof(Vector3))), originalTransform.OwnType());
								originalTransform.Release();
							}
							IVariable obj = null;
							method.machine.AddExtern("VRCInstantiate.__Instantiate__UnityEngineGameObject__UnityEngineGameObject",
								() => (obj = method.GetTmpVariable(typeof(GameObject))),
								objOriginal.OwnType()
							);
							ITmpVariable transform = null;
							if(parent != null)
							{
								GetTransform(method, obj, ref transform);
								if(copyWorldTransform)
								{
									bool worldTransformCondition = !(worldPositionStays is IConstVariable);
									EmbedAddressLabel skipLabel = null;
									if(worldTransformCondition)
									{
										skipLabel = new EmbedAddressLabel();
										worldPositionStays.Allocate();
										method.machine.AddBranch(worldPositionStays, skipLabel);
									}
									method.machine.AddExtern("UnityEngineTransform.__set_localPosition__UnityEngineVector3__SystemVoid",
										transform.OwnType(), position.OwnType());
									method.machine.AddExtern("UnityEngineTransform.__set_localRotation__UnityEngineQuaternion__SystemVoid",
										transform.OwnType(), rotation.OwnType());
									method.machine.AddExtern("UnityEngineTransform.__set_localScale__UnityEngineVector3__SystemVoid",
										transform.OwnType(), scale.OwnType());
									if(worldTransformCondition)
									{
										method.machine.ApplyLabel(skipLabel);
									}
									else
									{
										worldPositionStays = method.machine.GetConstVariable(true);
									}
									method.machine.AddExtern("UnityEngineTransform.__SetParent__UnityEngineTransform_SystemBoolean__SystemVoid",
										transform.OwnType(), parent.OwnType(), worldPositionStays.UseType(typeof(bool)));
								}
								else
								{
									method.machine.AddExtern("UnityEngineTransform.__SetParent__UnityEngineTransform_SystemBoolean__SystemVoid",
										transform.OwnType(), parent.OwnType(), method.machine.GetConstVariable(false).OwnType());
								}
							}
							if(position != null)
							{
								GetTransform(method, obj, ref transform);
								method.machine.AddExtern("UnityEngineTransform.__set_position__UnityEngineVector3__SystemVoid",
									transform.OwnType(), position.OwnType());
								method.machine.AddExtern("UnityEngineTransform.__set_rotation__UnityEngineQuaternion__SystemVoid",
									transform.OwnType(), rotation.OwnType());
							}
							transform?.Release();

							if(type == typeof(GameObject))
							{
								method.PushStack(obj);
							}
							else
							{
								var components = method.GetTmpVariable(typeof(Component[]));
								method.machine.AddExtern("UnityEngineGameObject.__GetComponents__SystemType__UnityEngineComponentArray",
									components, obj.OwnType(), method.machine.GetConstVariable(typeof(Component), typeof(Type)).OwnType());
								var component = method.GetTmpVariable(type);
								method.machine.AddExtern("UnityEngineComponentArray.__Get__SystemInt32__UnityEngineComponent",
									component, components.OwnType(), componentIndex.OwnType());
								method.PushStack(component);
							}
						}
						return true;
					}
					else ThrowException(type);
				}
				else ThrowException(typeof(UnityEngine.Object));
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new CallInstantiate();
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
		}

		private static void GetTransform(IMethodDescriptor method, IVariable obj, ref ITmpVariable transform)
		{
			if(transform == null)
			{
				transform = method.GetTmpVariable(typeof(Transform));
				transform.Reserve();
				obj.Allocate();
				method.machine.AddExtern("UnityEngineGameObject.__get_transform__UnityEngineTransform", transform, obj.OwnType());
			}
		}

		private static bool HasParameter(ParameterInfo[] parameters, int index, Type type)
		{
			return index < parameters.Length && parameters[index].ParameterType == type;
		}

		private static void ThrowException(Type type)
		{
			throw new Exception(string.Format("Instantiating an object of type {0} is not supported, the object type must be GameObject or must inherit from Component.", type));
		}
	}
}