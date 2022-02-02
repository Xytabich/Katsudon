using Katsudon.Cache;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using VRC.Core;

namespace Katsudon.Builder.Helpers
{
	public static class UdonCacheHelper
	{
		private static IUdonPartsCache instance = null;
		public static IUdonPartsCache cache
		{
			get
			{
				if(instance == null)
				{
					var type = cacheAssembly.GetType("Katsudon.Cache.UdonPartsCache", false);
					if(type != null)
					{
						var cache = Activator.CreateInstance(type) as IUdonPartsCache;
						if(cache.version == SDKClientUtilities.GetSDKVersionDate())
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

		private static Assembly _cacheAssembly = null;
		private static Assembly cacheAssembly
		{
			get
			{
				if(_cacheAssembly == null)
				{
					foreach(var asm in AppDomain.CurrentDomain.GetAssemblies())
					{
						if(asm.GetName().Name.StartsWith("Assembly-CSharp"))
						{
							if(asm.GetType("Katsudon.Cache.UdonPartsCollector", false) != null)
							{
								_cacheAssembly = asm;
								break;
							}
						}
					}
					if(_cacheAssembly == null) throw new Exception("Assembly with Katsudon cache not found");
				}
				return _cacheAssembly;
			}
		}

		[MenuItem("Katsudon/Refresh Udon Cache")]
		private static IUdonPartsCache CreateCache()
		{
			UdonPartsCacheBase collector = (UdonPartsCacheBase)Activator.CreateInstance(cacheAssembly.GetType("Katsudon.Cache.UdonPartsCollector"));

			EditorUtility.DisplayProgressBar("Katsudon caching", "Collecting supported types", 0.1f);
			var typeNames = collector.GetTypeNames();

			EditorUtility.DisplayProgressBar("Katsudon caching", "Collecting supported actions", 0.2f);
			var ctorNames = collector.GetCtorNames();

			var sb = new StringBuilder();
			int indent = 0;
			AppendLine(sb, indent, "using System;");
			AppendLine(sb, indent, "using System.Collections.Generic;");
			AppendLine(sb, indent, "using Katsudon.Builder.Helpers;");
			AppendLine(sb, indent, "namespace Katsudon.Cache {");
			{
				indent++;
				AppendLine(sb, indent, "internal class UdonPartsCache : UdonPartsCacheBase {");
				{
					indent++;
					AppendLineFormat(sb, indent, "public override string version => @\"{0}\";", SDKClientUtilities.GetSDKVersionDate());

					AppendLine(sb, indent, "public override string GetDirectoryPath() {");
					{
						indent++;
						AppendLine(sb, indent, "return System.IO.Path.GetDirectoryName(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileName());");
						indent--;
					}
					AppendLine(sb, indent, "}");

					AppendLine(sb, indent, "protected override void CreateCtorsList() {");
					{
						indent++;
						EditorUtility.DisplayProgressBar("Katsudon caching", "Building ctor list", 0.3f);
						AppendListBuilder(sb, indent, "ctorNames", "Dictionary<MethodIdentifier, string>", ctorNames, MethodNamesBuilder);
						indent--;
					}
					AppendLine(sb, indent, "}");

					AppendLine(sb, indent, "protected override void CreateFieldsList() {");
					{
						indent++;
						var fieldNames = collector.GetFieldNames();
						EditorUtility.DisplayProgressBar("Katsudon caching", "Building fields list", 0.4f);
						AppendListBuilder(sb, indent, "fieldNames", "Dictionary<FieldIdentifier, FieldNameInfo>", fieldNames, FieldNamesBuilder);
						indent--;
					}
					AppendLine(sb, indent, "}");

					AppendLine(sb, indent, "protected override void CreateMethodsList() {");
					{
						indent++;
						EditorUtility.DisplayProgressBar("Katsudon caching", "Building methods list", 0.5f);
						var methodNames = collector.GetMethodNames();
						AppendListBuilder(sb, indent, "methodNames", "Dictionary<MethodIdentifier, string>", methodNames, MethodNamesBuilder);
						indent--;
					}
					AppendLine(sb, indent, "}");

					AppendLine(sb, indent, "protected override void CreateMethodBaseTypesList() {");
					{
						indent++;
						EditorUtility.DisplayProgressBar("Katsudon caching", "Building methods list", 0.6f);
						var methodBaseTypes = collector.GetMethodBaseTypes();
						AppendListBuilder(sb, indent, "methodBaseTypes", "Dictionary<MethodIdentifier, Type[]>", methodBaseTypes, MethodBaseTypesBuilder);
						indent--;
					}
					AppendLine(sb, indent, "}");

					AppendLine(sb, indent, "protected override void CreateMagicMethodsList() {");
					{
						indent++;
						EditorUtility.DisplayProgressBar("Katsudon caching", "Building magic methods list", 0.7f);
						var magicMethods = collector.GetMagicMethods();
						AppendListBuilder(sb, indent, "magicMethods", "Dictionary<string, MagicMethodInfo>", magicMethods, MagicMethodsBuilder);
						indent--;
					}
					AppendLine(sb, indent, "}");

					AppendLine(sb, indent, "protected override void CreateTypesList() {");
					{
						indent++;
						EditorUtility.DisplayProgressBar("Katsudon caching", "Building types list", 0.8f);
						AppendListBuilder(sb, indent, "typeNames", "Dictionary<Type, string>", typeNames, TypeNamesBuilder);
						indent--;
					}
					AppendLine(sb, indent, "}");

					AppendLine(sb, indent, "protected override void CreateTypeIdentifiersList() {");
					{
						indent++;
						EditorUtility.DisplayProgressBar("Katsudon caching", "Building type identifiers list", 0.9f);
						var typeIdentifiers = collector.GetTypeIdentifiers();
						AppendLineFormat(sb, indent, "tmpTypeCounter = {0};", typeIdentifiers.Count);
						AppendListBuilder(sb, indent, "typeIdentifiers", "Dictionary<Type, int>", typeIdentifiers, TypeIdentifiersBuilder);
						indent--;
					}
					AppendLine(sb, indent, "}");
					indent--;
				}
				AppendLine(sb, indent, "}");
				indent--;
			}
			sb.Append('}');

			EditorUtility.DisplayProgressBar("Katsudon caching", "Saving cache", 1.0f);
			File.WriteAllText(Path.Combine(collector.GetDirectoryPath(), "UdonPartsCache.cs"), sb.ToString(), Encoding.UTF8);
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

		private static void AppendListBuilder<T>(StringBuilder sb, int indent, string outName,
			string typeName, IReadOnlyCollection<T> list, Action<StringBuilder, T> builder)
		{
			sb.Append('\t', indent);
			sb.Append("var local = new ");
			sb.Append(typeName);
			sb.Append("(");
			sb.Append(list.Count);
			sb.AppendLine(");");
			if(list.Count <= 1000)
			{
				foreach(var value in list)
				{
					sb.Append('\t', indent);
					sb.Append("local.Add(");
					builder.Invoke(sb, value);
					sb.AppendLine(");");
				}
			}
			else
			{
				// Splitting into multiple methods as one big method causes stack overflow
				bool methodStarted = false;
				int counter = 0, methodIndex = 0;
				foreach(var value in list)
				{
					if(!methodStarted)
					{
						methodStarted = true;
						sb.Append('\t', indent);
						sb.Append("void AddToList");
						sb.Append(methodIndex);
						sb.AppendLine("() {");
						indent++;
					}

					sb.Append('\t', indent);
					sb.Append("local.Add(");
					builder.Invoke(sb, value);
					sb.AppendLine(");");

					counter++;
					if(counter >= 500)
					{
						counter = 0;
						indent--;
						sb.Append('\t', indent);
						sb.AppendLine("}");
						sb.Append('\t', indent);
						sb.Append("AddToList");
						sb.Append(methodIndex);
						sb.AppendLine("();");
						methodIndex++;
						methodStarted = false;
					}
				}
				if(methodStarted)
				{
					indent--;
					sb.Append('\t', indent);
					sb.AppendLine("}");
					sb.Append('\t', indent);
					sb.Append("AddToList");
					sb.Append(methodIndex);
					sb.AppendLine("();");
				}
			}
			sb.Append('\t', indent);
			sb.Append(outName);
			sb.AppendLine(" = local;");
		}

		private static void FieldNamesBuilder(StringBuilder sb, KeyValuePair<FieldIdentifier, FieldNameInfo> pair)
		{
			pair.Key.AppendCtor(sb);
			sb.Append(", ");
			pair.Value.AppendCtor(sb);
		}

		private static void MethodNamesBuilder(StringBuilder sb, KeyValuePair<MethodIdentifier, string> pair)
		{
			pair.Key.AppendCtor(sb);
			sb.Append(", \"");
			sb.Append(pair.Value);
			sb.Append("\"");
		}

		private static void MethodBaseTypesBuilder(StringBuilder sb, KeyValuePair<MethodIdentifier, Type[]> pair)
		{
			pair.Key.AppendCtor(sb);
			sb.Append(", new Type[] { ");
			var types = pair.Value;
			for(int i = 0; i < types.Length; i++)
			{
				if(i > 0) sb.Append(", ");
				Utils.AppendTypeof(sb, types[i]);
			}
			sb.Append(" }");
		}

		private static void MagicMethodsBuilder(StringBuilder sb, KeyValuePair<string, MagicMethodInfo> pair)
		{
			sb.Append("\"");
			sb.Append(pair.Key);
			sb.Append("\", ");
			pair.Value.AppendCtor(sb);
		}

		private static void TypeNamesBuilder(StringBuilder sb, KeyValuePair<Type, string> pair)
		{
			Utils.AppendTypeof(sb, pair.Key);
			sb.Append(", \"");
			sb.Append(pair.Value);
			sb.Append("\"");
		}

		private static void TypeIdentifiersBuilder(StringBuilder sb, KeyValuePair<Type, int> pair)
		{
			Utils.AppendTypeof(sb, pair.Key);
			sb.Append(", ");
			sb.Append(pair.Value);
		}
	}
}