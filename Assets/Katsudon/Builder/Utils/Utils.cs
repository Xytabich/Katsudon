using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Katsudon.Builder;
using Katsudon.Builder.Helpers;

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
			return GetExternName(type, string.Format(externFormat, System.Array.ConvertAll(types, t => GetNameOrThrow(typeNames, t))));
		}

		public static bool IsUdonType(Type type)
		{
			return UdonCacheHelper.cache.ContainsUdonType(type);
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
			if(!method.TryGetNextSt(out variable))
			{
				variable = method.GetTmpVariable(type);
				if(addAllocate > 0) variable.Allocate(addAllocate);
				method.PushStack(variable);
			}
			return variable;
		}

		public static bool TryGetNextSt(this IMethodDescriptor method, out IVariable variable)
		{
			method.PushState();
			method.Next();
			var op = method.currentOp;
			int argIndex;
			if(!method.isStatic)
			{
				if(ILUtils.TryGetLdarg(op, out argIndex))
				{
					if(argIndex == 0)
					{
						method.PushState();
						if(method.Next())
						{
							FieldInfo field;
							if(ILUtils.TryGetStfld(method.currentOp, out field))
							{
								variable = method.machine.GetFieldsCollection().GetField(field);
								method.DropState();
								return true;
							}
						}
						method.PopState();
					}
				}
				else if(!method.stackIsEmpty && method.PeekStack(0) is ThisVariable)
				{
					FieldInfo field;
					if(ILUtils.TryGetStfld(op, out field))
					{
						variable = method.machine.GetFieldsCollection().GetField(field);
						method.DropState();
						method.PopStack().Use();
						return true;
					}
				}
			}
			if(ILUtils.TryGetStarg(op, out argIndex))
			{
				method.DropState();
				variable = method.GetArgumentVariable(argIndex);
				return true;
			}
			int locIndex;
			if(ILUtils.TryGetStloc(op, out locIndex))
			{
				method.DropState();
				variable = method.GetLocalVariable(locIndex);
				return true;
			}
			if(op.opCode == OpCodes.Ret)
			{
				method.PopState();
				variable = method.GetReturnVariable();
				return true;
			}
			method.PopState();
			variable = null;
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