using System;
using System.Collections.Generic;
using System.Reflection;

namespace Katsudon.Builder.Helpers
{
	public interface IUdonPartsCache
	{
		string version { get; }

		MethodIdentifier GetMethodIdentifier(MethodInfo info);

		MethodIdentifier GetCtorIdentifier(ConstructorInfo info);

		FieldIdentifier GetFieldIdentifier(FieldInfo info);

		int GetTypeIdentifier(Type type);

		GenericTypeIndexer GetGenericTypeIndexer();

		IReadOnlyDictionary<Type, string> GetTypeNames();

		bool ContainsUdonType(Type type);

		IReadOnlyDictionary<MethodIdentifier, string> GetMethodNames();

		IReadOnlyDictionary<MethodIdentifier, Type[]> GetMethodBaseTypes();

		IReadOnlyDictionary<MethodIdentifier, string> GetCtorNames();

		IReadOnlyDictionary<FieldIdentifier, FieldNameInfo> GetFieldNames();

		IReadOnlyDictionary<string, MagicMethodInfo> GetMagicMethods();
	}
}