using System;
using System.Linq.Expressions;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.ProgramSources;

namespace Katsudon.Editor.Udon
{
	public static class ProgramUtils
	{
		public delegate SerializedUdonProgramAsset UdonProgramFieldGetter(UdonBehaviour behaviour);
		private static UdonProgramFieldGetter _programFieldGetter = null;
		public static UdonProgramFieldGetter ProgramFieldGetter
		{
			get
			{
				if(_programFieldGetter == null)
				{
					var field = typeof(UdonBehaviour).GetField("serializedProgramAsset", BindingFlags.NonPublic | BindingFlags.Instance);
					var inputBehaviour = Expression.Parameter(typeof(UdonBehaviour));
					_programFieldGetter = Expression.Lambda<UdonProgramFieldGetter>(
						Expression.TypeAs(Expression.Field(inputBehaviour, field), typeof(SerializedUdonProgramAsset)),
						inputBehaviour
					).Compile();
				}
				return _programFieldGetter;
			}
		}

		public static MonoScript GetScriptByProgram(SerializedUdonProgramAsset program)
		{
			if(EditorUtility.IsPersistent(program))
			{
				var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(program));
				var map = importer.GetExternalObjectMap();
				if(map.TryGetValue(GetScriptIdentifier(), out var obj))
				{
					if(obj == null) Debug.LogWarningFormat("No MonoBehaviour found for program {0}, please provide a link to the script for normal work", program);
					return (MonoScript)obj;
				}
			}
			return null;
		}

		public static bool HasScriptRecord(SerializedUdonProgramAsset program)
		{
			if(EditorUtility.IsPersistent(program))
			{
				var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(program));
				return importer.GetExternalObjectMap().ContainsKey(GetScriptIdentifier());
			}
			return false;
		}

		public static SerializedUdonProgramAsset GetProgramByScript(MonoScript script)
		{
			if(EditorUtility.IsPersistent(script))
			{
				var type = script.GetClass();
				if(type != null)
				{
					var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(script));
					var map = importer.GetExternalObjectMap();
					if(map.TryGetValue(GetProgramIdentifier(script), out var obj))
					{
						return (SerializedUdonProgramAsset)obj;
					}
				}
			}
			return null;
		}

		public static SerializedUdonProgramAsset GetProgramByBehaviour(MonoBehaviour behaviour)
		{
			return GetProgramByScript(MonoScript.FromMonoBehaviour(behaviour));
		}

		public static AssetImporter.SourceAssetIdentifier GetProgramIdentifier(MonoScript script)
		{
			return AssetDatabase.IsMainAsset(script) ? GetMainProgramIdentifier() : GetSubProgramIdentifier(script.GetClass());
		}

		internal static AssetImporter.SourceAssetIdentifier GetMainProgramIdentifier()
		{
			return new AssetImporter.SourceAssetIdentifier(typeof(SerializedUdonProgramAsset), "Katsudon-Program");
		}

		internal static AssetImporter.SourceAssetIdentifier GetSubProgramIdentifier(Type scriptType)
		{
			return new AssetImporter.SourceAssetIdentifier(typeof(SerializedUdonProgramAsset), "Katsudon-" + scriptType.ToString() + "-Program");
		}

		public static AssetImporter.SourceAssetIdentifier GetScriptIdentifier()
		{
			return new AssetImporter.SourceAssetIdentifier(typeof(MonoScript), "Katsudon-Script");
		}
	}
}