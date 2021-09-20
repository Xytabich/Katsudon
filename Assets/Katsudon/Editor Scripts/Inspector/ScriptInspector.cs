using Katsudon.Editor.Udon;
using UnityEditor;
using UnityEngine;
using VRC.Udon.ProgramSources;

namespace Katsudon.Editor
{
	[CustomEditor(typeof(MonoScript)), CanEditMultipleObjects]
	public sealed class ScriptInspector : EditorProxyDrawer
	{
		private SerializedUdonProgramAsset program = null;
		private bool hasProgramRecord = false;

		protected override void OnInit()
		{
			var fallback = EditorReplacer.GetFallbackEditor(typeof(MonoScript), targets.Length > 1);
			base.CreateEditor(fallback);
			if(targets.Length == 1)
			{
				program = ProgramUtils.GetProgramByScript(targets[0] as MonoScript);
				if(program == null)
				{
					hasProgramRecord = ProgramUtils.HasProgramRecord(targets[0] as MonoScript);
				}
				else hasProgramRecord = true;
			}
		}

		public override void OnInspectorGUI()
		{
			if(hasProgramRecord)
			{
				using(new EditorGUI.DisabledScope(true))
				{
					EditorGUILayout.ObjectField("Udon program", program, typeof(SerializedUdonProgramAsset), false);
				}
				if(GUILayout.Button("Clear udon program meta"))
				{
					var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(targets[0]));
					foreach(var pair in importer.GetExternalObjectMap())
					{
						if(ProgramUtils.IsProgramRecord(pair.Key))
						{
							importer.RemoveRemap(pair.Key);
						}
					}
					importer.SaveAndReimport();
				}
			}
			base.OnInspectorGUI();
		}
	}
}