using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder;
using Katsudon.Builder.Helpers;
using UnityEngine;

namespace Katsudon
{
	public static class Utils
	{
		public static string PrepareMemberName(string str)
		{
			var chars = str.ToCharArray();
			for(var i = 0; i < chars.Length; i++)
			{
				var c = chars[i];
				if(char.IsLetter(c)) continue;
				if(char.IsDigit(c)) continue;
				if(c == '_') continue;
				chars[i] = '_';
			}
			return new string(chars);
		}

		public static string PrepareFieldName(FieldInfo field)
		{
			return PrepareMemberName(field.Name);
		}

		public static string PrepareMethodName(MethodInfo method)
		{
			return PrepareMemberName(method.Name);
		}

		public static string PrepareInterfaceMethodName(MethodInfo method)
		{
			return PrepareMemberName(string.Format("{0}__{1}", method.DeclaringType, method.Name));
		}

		public static MethodInfo GetPropertyMethod<T>(string name, bool getter = true)
		{
			var property = typeof(T).GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
			return getter ? property.GetGetMethod() : property.GetSetMethod();
		}

		public static MethodInfo GetMethod<T>(string name, params Type[] types)
		{
			return typeof(T).GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, types, null);
		}

		public static string GetExternName(Type type, string externName)
		{
			return string.Format("{0}.{1}", GetNameOrThrow(UdonCacheHelper.cache.GetTypeNames(), type), externName);
		}

		public static string GetExternName(Type type, string externFormat, params Type[] types)
		{
			var typeNames = UdonCacheHelper.cache.GetTypeNames();
			return GetExternName(type, string.Format(externFormat, Array.ConvertAll(types, t => GetNameOrThrow(typeNames, t))));
		}

		public static bool IsUdonType(Type type)
		{
			return UdonCacheHelper.cache.ContainsUdonType(type);
		}

		public static bool TryFindUdonMethod(this IUdonPartsCache cache, Type calleeType, MethodInfo method, out MethodIdentifier id, out string fullName)
		{
			var names = cache.GetMethodNames();
			id = new MethodIdentifier(cache, method);
			if(names.TryGetValue(id, out fullName)) return true;
			if(calleeType == null || !method.DeclaringType.IsAssignableFrom(calleeType)) return false;

			var baseType = method.DeclaringType;
			while(calleeType != baseType && calleeType != null)
			{
				id = new MethodIdentifier(cache, calleeType, method);
				if(names.TryGetValue(id, out fullName)) return true;
				calleeType = calleeType.BaseType;
			}
			return false;
		}

		public static T Used<T>(this T variable) where T : IVariable
		{
			variable.Use();
			return variable;
		}

		public static T Used<T>(this T variable, int allocate) where T : IVariable
		{
			variable.Allocate(allocate);
			variable.Use();
			return variable;
		}

		public static IVariable GetOrPushOutVariable(this IMethodDescriptor method, Type type, int addAllocate = 0)
		{
			IVariable variable;
			if(!method.TryGetNextSt(type, out variable))
			{
				variable = method.GetTmpVariable(type);
				if(addAllocate > 0) variable.Allocate(addAllocate);
				method.PushStack(variable);
			}
			return variable;
		}

		public static bool TryGetNextSt(this IMethodDescriptor method, Type targetType, out IVariable variable)
		{
			variable = null;
			var handle = method.GetStateHandle();
			handle.Next();
			var op = method.currentOp;
			int argIndex;
			if(method.isBehaviour)
			{
				if(ILUtils.TryGetLdarg(op, out argIndex, !method.isStatic))
				{
					if(argIndex < 0)
					{
						var argHandle = method.GetStateHandle();
						if(argHandle.Next())
						{
							FieldInfo field;
							if(ILUtils.TryGetStfld(method.currentOp, out field))
							{
								if(!field.FieldType.IsAssignableFrom(targetType))
								{
									argHandle.Drop();
									return false;
								}

								variable = (method.machine as IRawUdonMachine).mainMachine.GetFieldsCollection().GetField(field);
								argHandle.Apply();
								return true;
							}
						}
						argHandle.Drop();
					}
				}
				else if(!method.stackIsEmpty && method.PeekStack(0) is ThisVariable)
				{
					FieldInfo field;
					if(ILUtils.TryGetStfld(op, out field))
					{
						if(!field.FieldType.IsAssignableFrom(targetType))
						{
							handle.Drop();
							return false;
						}

						variable = (method.machine as IRawUdonMachine).mainMachine.GetFieldsCollection().GetField(field);
						handle.Apply();
						method.PopStack().Use();
						return true;
					}
				}
			}
			if(ILUtils.TryGetStarg(op, out argIndex, !method.isStatic))
			{
				variable = method.GetArgumentVariable(argIndex);
				if(!variable.type.IsAssignableFrom(targetType))
				{
					handle.Drop();
					return false;
				}

				handle.Apply();
				return true;
			}
			int locIndex;
			if(ILUtils.TryGetStloc(op, out locIndex))
			{
				variable = method.GetLocalVariable(locIndex);
				if(!variable.type.IsAssignableFrom(targetType))
				{
					handle.Drop();
					return false;
				}

				handle.Apply();
				return true;
			}
			if(op.opCode == OpCodes.Ret)
			{
				variable = method.GetReturnVariable();
				if(!variable.type.IsAssignableFrom(targetType))
				{
					handle.Drop();
					return false;
				}

				handle.Drop();
				return true;
			}
			handle.Drop();
			return false;
		}

		public static bool IsNativeTypeCode(TypeCode code)
		{
			switch(code)
			{
				case TypeCode.Object:
				case TypeCode.Empty:
				case TypeCode.DBNull:
					return false;
				default:
					return true;
			}
		}

		public static bool IsUdonAsm(Type type)
		{
			return type.Assembly.IsDefined(typeof(UdonAsmAttribute));
		}

		public static bool IsUdonAsmBehaviour(Type type)
		{
			return type.Assembly.IsDefined(typeof(UdonAsmAttribute)) && typeof(MonoBehaviour).IsAssignableFrom(type);
		}

		public static bool IsUdonAsmBehaviourOrInterface(Type type)
		{
			return type.Assembly.IsDefined(typeof(UdonAsmAttribute)) && (type.IsInterface || typeof(MonoBehaviour).IsAssignableFrom(type));
		}

		public static bool IsStruct(Type type)
		{
			return !type.IsAbstract && type.IsClass && type.BaseType == typeof(object);
		}

		public static bool IsUdonAsmStruct(Type type)
		{//TODO: full check & cache result
			return type.Assembly.IsDefined(typeof(UdonAsmAttribute)) && !type.IsAbstract && type.IsClass && type.BaseType == typeof(object);
		}

		public static bool IsUdonAsm(Assembly assembly)
		{
			return assembly.IsDefined(typeof(UdonAsmAttribute));
		}

		private static string GetNameOrThrow(IReadOnlyDictionary<Type, string> list, Type type)
		{
			if(list.TryGetValue(type, out var name)) return name;
			throw new Exception(string.Format("The type {0} is not supported by UDON", type));
		}
	}
}