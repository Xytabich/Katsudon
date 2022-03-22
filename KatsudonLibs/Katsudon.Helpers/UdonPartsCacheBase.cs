using Katsudon.Builder.Helpers;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Katsudon.Cache
{
	public abstract class UdonPartsCacheBase : IUdonPartsCache
	{
		public abstract string version { get; }

		protected IReadOnlyDictionary<Type, string> typeNames = null;
		protected IReadOnlyDictionary<MethodIdentifier, string> methodNames = null;
		protected IReadOnlyDictionary<MethodIdentifier, string> ctorNames = null;
		protected IReadOnlyDictionary<FieldIdentifier, FieldNameInfo> fieldNames = null;
		protected IReadOnlyDictionary<string, MagicMethodInfo> magicMethods = null;

		protected IReadOnlyDictionary<MethodIdentifier, Type[]> methodBaseTypes = null;

		protected ICollection<string> externNames = null;

		protected int tmpTypeCounter;
		protected Dictionary<Type, int> typeIdentifiers = null;

		private Dictionary<Type, int> cachedGenericTypes = new Dictionary<Type, int>();

		public IReadOnlyDictionary<Type, int> GetTypeIdentifiers()
		{
			return typeIdentifiers;
		}

		public MethodIdentifier GetMethodIdentifier(MethodInfo info)
		{
			return new MethodIdentifier(this, info);
		}

		public MethodIdentifier GetCtorIdentifier(ConstructorInfo info)
		{
			return new MethodIdentifier(this, info);
		}

		public FieldIdentifier GetFieldIdentifier(FieldInfo info)
		{
			return new FieldIdentifier(this, info);
		}

		public int GetTypeIdentifier(Type type)
		{
			if(typeIdentifiers == null) CreateTypeIdentifiersList();

			int id;
			if(typeIdentifiers.TryGetValue(type, out id)) return id;

			id = tmpTypeCounter++;
			typeIdentifiers[type] = id;
			return id;
		}

		public GenericTypeIndexer GetGenericTypeIndexer()
		{
			cachedGenericTypes.Clear();
			return new GenericTypeIndexer(cachedGenericTypes);
		}

		public IReadOnlyDictionary<Type, string> GetTypeNames()
		{
			if(typeNames == null) CreateTypesList();
			return typeNames;
		}

		public bool ContainsUdonType(Type type)
		{
			if(typeNames == null) CreateTypesList();
			return typeNames.ContainsKey(type);
		}

		public bool ContainsExtern(string fullName)
		{
			if(externNames == null) CreateExternsList();
			return externNames.Contains(fullName);
		}

		public IReadOnlyCollection<string> GetExternNames()
		{
			if(externNames == null) CreateExternsList();
			return (IReadOnlyCollection<string>)externNames;
		}

		public IReadOnlyDictionary<MethodIdentifier, string> GetMethodNames()
		{
			if(methodNames == null) CreateMethodsList();
			return methodNames;
		}

		public IReadOnlyDictionary<MethodIdentifier, Type[]> GetMethodBaseTypes()
		{
			if(methodBaseTypes == null) CreateMethodBaseTypesList();
			return methodBaseTypes;
		}

		public IReadOnlyDictionary<MethodIdentifier, string> GetCtorNames()
		{
			if(ctorNames == null) CreateCtorsList();
			return ctorNames;
		}

		public IReadOnlyDictionary<FieldIdentifier, FieldNameInfo> GetFieldNames()
		{
			if(fieldNames == null) CreateFieldsList();
			return fieldNames;
		}

		public IReadOnlyDictionary<string, MagicMethodInfo> GetMagicMethods()
		{
			if(magicMethods == null) CreateMagicMethodsList();
			return magicMethods;
		}

		public abstract string GetDirectoryPath();

		protected abstract void CreateTypesList();

		protected abstract void CreateExternsList();

		protected abstract void CreateMethodsList();

		protected abstract void CreateMethodBaseTypesList();

		protected abstract void CreateCtorsList();

		protected abstract void CreateFieldsList();

		protected abstract void CreateMagicMethodsList();

		protected abstract void CreateTypeIdentifiersList();
	}
}