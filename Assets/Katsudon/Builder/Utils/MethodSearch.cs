using System;
using System.Reflection;

namespace Katsudon.Utility
{
	public static class MethodSearch<T> where T : Delegate
	{
		private static readonly Type[] parameters = Array.ConvertAll(typeof(T).GetMethod("Invoke").GetParameters(), p => p.ParameterType);

		public static MethodInfo FindInstanceMethod(Type type, string name)
		{
			return type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, parameters, null);
		}

		public static MethodInfo FindStaticMethod(Type type, string name)
		{
			return type.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, parameters, null);
		}
	}
}