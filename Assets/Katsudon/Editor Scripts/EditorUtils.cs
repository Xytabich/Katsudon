using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace Katsudon.Editor
{
	internal static class EditorUtils
	{
		private static Func<string> GetActiveFolderPath = (Func<string>)Delegate.CreateDelegate(typeof(Func<string>),
			typeof(ProjectWindowUtil).GetMethod("GetActiveFolderPath", BindingFlags.NonPublic | BindingFlags.Static));

		[MenuItem("Assets/Create/UDON Assembly Folder", true, 20)]
		private static bool CanCreateAssemblyFolder()
		{
			return GetActiveFolderPath().StartsWith("Assets");
		}

		[MenuItem("Assets/Create/UDON Assembly Folder", false, 20)]
		private static void CreateAssemblyFolder()
		{
			ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, ScriptableObject.CreateInstance<DoCreateAssemblyFolder>(), "New Assembly", EditorGUIUtility.IconContent(EditorResources.emptyFolderIconName).image as Texture2D, null);
		}

		private class DoCreateAssemblyFolder : EndNameEditAction
		{
			public override void Action(int instanceId, string pathName, string resourceFile)
			{
				var name = Path.GetFileName(pathName);
				AssetDatabase.CreateFolder(Path.GetDirectoryName(pathName), name);

				var cscPath = Path.Combine(pathName, "csc.rsp");
				File.WriteAllText(cscPath, "-checked\n-optimize");

				var infoPath = Path.Combine(pathName, "AssemblyInfo.cs");
				File.WriteAllText(infoPath, "[assembly: " + typeof(UdonAsmAttribute) + "]");

				var definitionPath = Path.Combine(pathName, name + ".asmdef");
				File.WriteAllText(definitionPath, JsonUtility.ToJson(new AssemblyDefinition(name, "Katsudon.Runtime")));

				AssetDatabase.Refresh();
				ProjectWindowUtil.ShowCreatedAsset(AssetDatabase.LoadAssetAtPath(definitionPath, typeof(UnityEngine.Object)));
			}

			private struct AssemblyDefinition
			{
				public string name;
				public string[] references;

				public AssemblyDefinition(string name, params string[] references)
				{
					this.name = name;
					this.references = references;
				}
			}
		}
	}
}