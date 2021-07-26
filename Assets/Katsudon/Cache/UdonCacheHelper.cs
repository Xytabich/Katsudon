using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Katsudon.Builder.Helpers
{
	public class UdonCacheHelper
	{
		private static IUdonPartsCache instance = null;
		public static IUdonPartsCache cache
		{
			get
			{
				if(instance == null)
				{
					var type = typeof(UdonCacheHelper).Assembly.GetType("Katsudon.Builder.Helpers.UdonPartsCache", false);
					if(type != null)
					{
						var cache = Activator.CreateInstance(type) as IUdonPartsCache;
						if(cache.version == GetUdonVersion())
						{
							instance = cache;
							return cache;
						}
					}
					instance = CreateCache();
				}
				return instance;
			}
		}

		[MenuItem("Katsudon/Refresh Udon Cache")]
		private static IUdonPartsCache CreateCache()
		{
			var collector = new UdonPartsCollector();

			EditorUtility.DisplayProgressBar("Katsudon caching", "Collecting supported types", 0.1f);
			var typeNames = collector.GetTypeNames();

			EditorUtility.DisplayProgressBar("Katsudon caching", "Collecting supported actions", 0.2f);
			var ctorNames = collector.GetCtorNames();

			var sb = new StringBuilder();
			int indent = 0;
			AppendLine(sb, indent, "using System;");
			AppendLine(sb, indent, "using System.Collections.Generic;");
			AppendLine(sb, indent, "namespace Katsudon.Builder.Helpers {");
			{
				indent++;
				AppendLine(sb, indent, "internal class UdonPartsCache : UdonPartsCacheBase {");
				{
					indent++;
					AppendLineFormat(sb, indent, "public override string version => @\"{0}\";", GetUdonVersion());

					AppendLine(sb, indent, "protected override void CreateCtorsList() {");
					{
						indent++;
						EditorUtility.DisplayProgressBar("Katsudon caching", "Building ctor list", 0.3f);
						AppendLineFormat(sb, indent, "ctorNames = new Dictionary<MethodIdentifier, string>({0}) {{", ctorNames.Count);
						indent++;
						foreach(var pair in ctorNames)
						{
							sb.Append('\t', indent);
							sb.Append("{ ");
							pair.Key.AppendCtor(sb);
							sb.Append(", \"");
							sb.Append(pair.Value);
							sb.Append("\" },\n");
						}
						indent--;
						AppendLine(sb, indent, "};");
						indent--;
					}
					AppendLine(sb, indent, "}");

					AppendLine(sb, indent, "protected override void CreateFieldsList() {");
					{
						indent++;
						var fieldNames = collector.GetFieldNames();
						EditorUtility.DisplayProgressBar("Katsudon caching", "Building fields list", 0.4f);
						AppendLineFormat(sb, indent, "fieldNames = new Dictionary<FieldIdentifier, FieldNameInfo>({0}) {{", fieldNames.Count);
						{
							indent++;
							foreach(var pair in fieldNames)
							{
								sb.Append('\t', indent);
								sb.Append("{ ");
								pair.Key.AppendCtor(sb);
								sb.Append(", ");
								pair.Value.AppendCtor(sb);
								sb.Append(" },\n");
							}
							indent--;
						}
						AppendLine(sb, indent, "};");
						indent--;
					}
					AppendLine(sb, indent, "}");

					AppendLine(sb, indent, "protected override void CreateMethodsList() {");
					{
						indent++;
						EditorUtility.DisplayProgressBar("Katsudon caching", "Building methods list", 0.5f);
						var methodNames = collector.GetMethodNames();
						AppendLineFormat(sb, indent, "methodNames = new Dictionary<MethodIdentifier, string>({0}) {{", methodNames.Count);
						{
							indent++;
							foreach(var pair in methodNames)
							{
								sb.Append('\t', indent);
								sb.Append("{ ");
								pair.Key.AppendCtor(sb);
								sb.Append(", \"");
								sb.Append(pair.Value);
								sb.Append("\" },\n");
							}
							indent--;
						}
						AppendLine(sb, indent, "};");
						indent--;
					}
					AppendLine(sb, indent, "}");

					AppendLine(sb, indent, "protected override void CreateMagicMethodsList() {");
					{
						indent++;
						EditorUtility.DisplayProgressBar("Katsudon caching", "Building magic methods list", 0.6f);
						var magicMethdos = collector.GetMagicMethods();
						AppendLineFormat(sb, indent, "magicMethods = new Dictionary<string, MagicMethodInfo>({0}) {{", magicMethdos.Count);
						{
							indent++;
							foreach(var pair in magicMethdos)
							{
								sb.Append('\t', indent);
								sb.Append("{ \"");
								sb.Append(pair.Key);
								sb.Append("\", ");
								pair.Value.AppendCtor(sb);
								sb.Append(" },\n");
							}
							indent--;
						}
						AppendLine(sb, indent, "};");
						indent--;
					}
					AppendLine(sb, indent, "}");

					AppendLine(sb, indent, "protected override void CreateTypesList() {");
					{
						indent++;
						EditorUtility.DisplayProgressBar("Katsudon caching", "Building types list", 0.7f);
						AppendLineFormat(sb, indent, "typeNames = new Dictionary<Type, string>({0}) {{", typeNames.Count);
						{
							indent++;
							foreach(var pair in typeNames)
							{
								sb.Append('\t', indent);
								sb.Append("{ ");
								AppendTypeof(sb, pair.Key);
								sb.Append(", \"");
								sb.Append(pair.Value);
								sb.Append("\" },\n");
							}
							indent--;
						}
						AppendLine(sb, indent, "};");
						indent--;
					}
					AppendLine(sb, indent, "}");

					AppendLine(sb, indent, "protected override void CreateTypeIdentifiersList() {");
					{
						indent++;
						EditorUtility.DisplayProgressBar("Katsudon caching", "Building type identifiers list", 0.8f);
						var typeIdentifiers = collector.GetTypeIdentifiers();
						AppendLineFormat(sb, indent, "tmpTypeCounter = {0};", typeIdentifiers.Count);
						AppendLineFormat(sb, indent, "typeIdentifiers = new Dictionary<Type, int>({0}) {{", typeIdentifiers.Count);
						{
							indent++;
							foreach(var pair in typeIdentifiers)
							{
								sb.Append('\t', indent);
								sb.Append("{ ");
								AppendTypeof(sb, pair.Key);
								sb.Append(", ");
								sb.Append(pair.Value);
								sb.Append(" },\n");
							}
							indent--;
						}
						AppendLine(sb, indent, "};");
						indent--;
					}
					AppendLine(sb, indent, "}");
					indent--;
				}
				AppendLine(sb, indent, "}");
				indent--;
			}
			sb.Append('}');

			EditorUtility.DisplayProgressBar("Katsudon caching", "Saving cache", 0.9f);
			File.WriteAllText(Path.Combine(Path.GetDirectoryName(new StackTrace(true).GetFrame(0).GetFileName()), "UdonPartsCache.cs"), sb.ToString(), Encoding.UTF8);
			EditorUtility.ClearProgressBar();
			return collector;
		}

		private static void AppendLine(StringBuilder sb, int indent, string text)
		{
			sb.Append('\t', indent);
			sb.Append(text);
			sb.Append('\n');
		}

		private static void AppendLineFormat(StringBuilder sb, int indent, string text, object arg0)
		{
			sb.Append('\t', indent);
			sb.AppendFormat(text, arg0);
			sb.Append('\n');
		}

		public static void AppendTypeof(StringBuilder sb, Type type)
		{
			sb.Append("typeof(");
			AppendTypeName(sb, type);
			sb.Append(')');
			if(type.IsByRef) sb.Append(".MakeByRefType()");
		}

		public static void AppendTypeName(StringBuilder sb, Type type)
		{
			if(type == typeof(void))
			{
				sb.Append("void");
				return;
			}

			if(type.IsByRef) type = type.GetElementType();
			if(type.IsArray)
			{
				AppendTypeName(sb, type.GetElementType());
				sb.Append('[');
				int rank = type.GetArrayRank();
				if(rank > 1) sb.Append(',', rank - 1);
				sb.Append(']');
				return;
			}
			if(type.IsNested)
			{
				sb.Append(type.DeclaringType);
				sb.Append('.');
			}
			else if(!string.IsNullOrEmpty(type.Namespace))
			{
				sb.Append(type.Namespace);
				sb.Append('.');
			}
			if(type.IsGenericType)
			{
				var definition = type.GetGenericTypeDefinition();
				var args = type.GetGenericArguments();

				var name = definition.Name;
				sb.Append(name.Remove(name.LastIndexOf('`')));
				sb.Append('<');
				for(var i = 0; i < args.Length; i++)
				{
					if(i > 0) sb.Append(", ");
					AppendTypeName(sb, args[i]);
				}
				sb.Append('>');
			}
			else
			{
				sb.Append(type.Name);
			}
		}

		private static string GetUdonVersion()
		{
			string currentVersion = "";
			string versionTextPath = Application.dataPath + "/Udon/version.txt";
			if(File.Exists(versionTextPath))
			{
				string[] versionFileLines = System.IO.File.ReadAllLines(versionTextPath);
				if(versionFileLines.Length > 0)
				{
					currentVersion = versionFileLines[0];
				}
			}
			return currentVersion;
		}
	}

	internal abstract class UdonPartsCacheBase : IUdonPartsCache
	{
		public abstract string version { get; }

		protected IReadOnlyDictionary<Type, string> typeNames = null;
		protected IReadOnlyDictionary<MethodIdentifier, string> methodNames = null;
		protected IReadOnlyDictionary<MethodIdentifier, string> ctorNames = null;
		protected IReadOnlyDictionary<FieldIdentifier, FieldNameInfo> fieldNames = null;
		protected IReadOnlyDictionary<string, MagicMethodInfo> magicMethods = null;

		protected int tmpTypeCounter;
		protected Dictionary<Type, int> typeIdentifiers = null;

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

		public IReadOnlyDictionary<MethodIdentifier, string> GetMethodNames()
		{
			if(methodNames == null) CreateMethodsList();
			return methodNames;
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

		protected abstract void CreateTypesList();

		protected abstract void CreateMethodsList();

		protected abstract void CreateCtorsList();

		protected abstract void CreateFieldsList();

		protected abstract void CreateMagicMethodsList();

		protected abstract void CreateTypeIdentifiersList();
	}

	public interface IUdonPartsCache
	{
		string version { get; }

		MethodIdentifier GetMethodIdentifier(MethodInfo info);

		MethodIdentifier GetCtorIdentifier(ConstructorInfo info);

		FieldIdentifier GetFieldIdentifier(FieldInfo info);

		int GetTypeIdentifier(Type type);

		IReadOnlyDictionary<Type, string> GetTypeNames();

		bool ContainsUdonType(Type type);

		IReadOnlyDictionary<MethodIdentifier, string> GetMethodNames();

		IReadOnlyDictionary<MethodIdentifier, string> GetCtorNames();

		IReadOnlyDictionary<FieldIdentifier, FieldNameInfo> GetFieldNames();

		IReadOnlyDictionary<string, MagicMethodInfo> GetMagicMethods();
	}

	public struct MethodIdentifier : IEquatable<MethodIdentifier>
	{
		private string name;
		private int declaringType;
		private int[] arguments;
		private int hashCode;

		public MethodIdentifier(IUdonPartsCache cache, MethodBase info)
		{
			name = info.Name;
			declaringType = cache.GetTypeIdentifier(info.DeclaringType);

			var parameters = info.GetParameters();
			arguments = new int[parameters.Length];
			for(var i = 0; i < parameters.Length; i++)
			{
				var type = parameters[i].ParameterType;
				if(type.IsByRef) type = type.GetElementType();
				arguments[i] = cache.GetTypeIdentifier(type);
			}

			hashCode = -890188069;
			hashCode = hashCode * -1521134295 + name.GetHashCode();
			hashCode = hashCode * -1521134295 + declaringType.GetHashCode();
			for(var i = 0; i < arguments.Length; i++)
			{
				hashCode = hashCode * -1521134295 + arguments[i].GetHashCode();
			}
		}

		public MethodIdentifier(int declaringType, string name, int[] arguments)
		{
			this.declaringType = declaringType;
			this.arguments = arguments;
			this.name = name;

			hashCode = -890188069;
			hashCode = hashCode * -1521134295 + name.GetHashCode();
			hashCode = hashCode * -1521134295 + declaringType.GetHashCode();
			for(var i = 0; i < arguments.Length; i++)
			{
				hashCode = hashCode * -1521134295 + arguments[i].GetHashCode();
			}
		}

		public override bool Equals(object obj)
		{
			return obj is MethodIdentifier identifier && Equals(identifier);
		}

		public bool Equals(MethodIdentifier other)
		{
			if(declaringType == other.declaringType && name == other.name)
			{
				var args = other.arguments;
				int len = arguments.Length;
				if(len == args.Length)
				{
					if(len > 0)
					{
						unsafe
						{
							fixed(int* a = arguments, b = args)
							{
								for(var i = 0; i < len; i++)
								{
									if(a[i] != b[i]) return false;
								}
							}
						}
					}
					return true;
				}
			}
			return false;
		}

		public override int GetHashCode()
		{
			return hashCode;
		}

		public void AppendCtor(StringBuilder sb)
		{
			sb.Append("new MethodIdentifier(");
			sb.Append(declaringType);
			sb.Append(", \"");
			sb.Append(name);
			sb.Append("\", new int[] {");
			for(var i = 0; i < arguments.Length; i++)
			{
				if(i > 0) sb.Append(',');
				sb.Append(' ');
				sb.Append(arguments[i]);
			}
			sb.Append(" })");
		}

		public static bool operator ==(MethodIdentifier a, MethodIdentifier b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(MethodIdentifier a, MethodIdentifier b)
		{
			return !a.Equals(b);
		}
	}

	public struct FieldIdentifier : IEquatable<FieldIdentifier>
	{
		private int declaringType;
		private string name;
		private int hashCode;

		public FieldIdentifier(IUdonPartsCache cache, FieldInfo info) : this(cache.GetTypeIdentifier(info.DeclaringType), info.Name) { }

		public FieldIdentifier(int declaringType, string name)
		{
			this.declaringType = declaringType;
			this.name = name;

			hashCode = -117268428;
			hashCode = hashCode * -1521134295 + declaringType.GetHashCode();
			hashCode = hashCode * -1521134295 + name.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			return obj is FieldIdentifier identifier && Equals(identifier);
		}

		public bool Equals(FieldIdentifier other)
		{
			return declaringType == other.declaringType && name == other.name;
		}

		public override int GetHashCode()
		{
			return hashCode;
		}

		public void AppendCtor(StringBuilder sb)
		{
			sb.Append("new FieldIdentifier(");
			sb.Append(declaringType);
			sb.Append(", \"");
			sb.Append(name);
			sb.Append("\")");
		}

		public static bool operator ==(FieldIdentifier a, FieldIdentifier b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(FieldIdentifier a, FieldIdentifier b)
		{
			return !a.Equals(b);
		}
	}

	public struct MagicMethodInfo
	{
		public string udonName;
		public Type returnType;
		public KeyValuePair<string, Type>[] parameters;

		public MagicMethodInfo(string udonName, Type returnType, KeyValuePair<string, Type>[] parameters)
		{
			this.udonName = udonName;
			this.returnType = returnType;
			this.parameters = parameters;
		}

		public void AppendCtor(StringBuilder sb)
		{
			sb.Append("new MagicMethodInfo(\"");
			sb.Append(udonName);
			sb.Append("\", ");
			UdonCacheHelper.AppendTypeof(sb, returnType);
			sb.Append(", new KeyValuePair<string, Type>[] {");
			for(var i = 0; i < parameters.Length; i++)
			{
				if(i > 0) sb.Append(',');
				sb.Append(" new KeyValuePair<string, Type>(\"");
				sb.Append(parameters[i].Key);
				sb.Append("\", ");
				UdonCacheHelper.AppendTypeof(sb, parameters[i].Value);
				sb.Append(")");
			}
			sb.Append(" })");
		}
	}

	public struct FieldNameInfo
	{
		public string getterName;
		public string setterName;

		public FieldNameInfo(string getterName, string setterName)
		{
			this.getterName = getterName;
			this.setterName = setterName;
		}

		public void AppendCtor(StringBuilder sb)
		{
			sb.Append("new FieldNameInfo(");
			if(string.IsNullOrEmpty(getterName)) sb.Append("null");
			else
			{
				sb.Append('"');
				sb.Append(getterName);
				sb.Append('"');
			}
			sb.Append(", ");
			if(string.IsNullOrEmpty(setterName)) sb.Append("null");
			else
			{
				sb.Append('"');
				sb.Append(setterName);
				sb.Append('"');
			}
			sb.Append(")");
		}
	}
}