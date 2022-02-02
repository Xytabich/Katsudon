using Katsudon.Editor.Udon;
using Katsudon.Helpers;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.Udon;
using VRC.Udon.ProgramSources;

namespace Katsudon.Editor
{
	[CustomEditor(typeof(SerializedUdonProgramAsset))]
	public class ProgramAssetInspector : EditorProxyDrawer
	{
		private bool hasScript = false;
		private MonoScript script = null;

		protected override void OnInit()
		{
			var fallback = EditorReplacer.GetFallbackEditor(typeof(SerializedUdonProgramAsset), targets.Length > 1);
			base.CreateEditor(fallback);

			var programImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(target));
			if(programImporter.GetExternalObjectMap().TryGetValue(ProgramUtils.GetScriptIdentifier(), out var obj))
			{
				hasScript = true;
				script = (MonoScript)obj;
			}
		}

		public override VisualElement CreateInspectorGUI()
		{
			var root = new VisualElement();
			root.style.overflow = Overflow.Visible;
			root.AddToClassList(InspectorElement.customInspectorUssClassName);
			root.AddToClassList(InspectorElement.iMGUIContainerUssClassName);
			if(hasScript) root.Add(new IMGUIContainer(ScriptField));
			root.Add(new Button(ShowDisassemblyWindow) { text = "Show disassembly" });
			root.Add(new IMGUIContainer(OnInspectorGUI));
			return root;
		}

		private void ScriptField()
		{
			if(script == null)
			{
				script = ReplaceMissingClassGUI(target as SerializedUdonProgramAsset);
			}
			else
			{
				using(new EditorGUI.DisabledScope(true))
				{
					EditorGUILayout.ObjectField("Class", script, typeof(MonoScript), false);
				}
			}
		}

		private void ShowDisassemblyWindow()
		{
			UdonProgramDisassemblyWindow.Show((target as AbstractSerializedUdonProgramAsset).RetrieveProgram());
		}

		internal static MonoScript ReplaceMissingClassGUI(SerializedUdonProgramAsset program)
		{
			MonoScript script = null;
			var programImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(program));
			if(programImporter.GetExternalObjectMap().TryGetValue(ProgramUtils.GetScriptIdentifier(), out var scriptAsset))
			{
				script = (MonoScript)scriptAsset;
			}

			var oldColor = GUI.color;
			GUI.color = new Color(1f, 0.5f, 0f);
			EditorGUI.BeginChangeCheck();
			script = (MonoScript)EditorGUILayout.ObjectField("Missing class", script, typeof(MonoScript), false);
			if(EditorGUI.EndChangeCheck() && script != null)
			{
				if(BuildTracker.IsValidScript(script))
				{
					AssetImporter scriptImporter;
					AssetImporter.SourceAssetIdentifier identifier;
					if(AssetDatabase.IsMainAsset(script))
					{
						scriptImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(script));
						identifier = ProgramUtils.GetMainProgramIdentifier();
					}
					else
					{
						AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string guid, out long fileId);
						scriptImporter = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
						identifier = ProgramUtils.GetSubProgramIdentifier(script.GetClass());
					}
					if(scriptImporter.GetExternalObjectMap().TryGetValue(identifier, out var programAsset))
					{
						if(programAsset != null)
						{
							if(programAsset != program)
							{
								if(EditorUtility.DisplayDialog("Script replacement", "The target script already contains a link to the program. Do you want to destroy the old program and replace the link with this one?", "Yes", "No"))
								{
									programImporter.AddRemap(ProgramUtils.GetScriptIdentifier(), script);
									programImporter.SaveAndReimport();
									scriptImporter.AddRemap(identifier, program);
									scriptImporter.SaveAndReimport();

									AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(programAsset));
								}
								else
								{
									script = null;
								}
							}
							else
							{
								programImporter.AddRemap(ProgramUtils.GetScriptIdentifier(), script);
								programImporter.SaveAndReimport();
							}
						}
						else
						{
							programImporter.AddRemap(ProgramUtils.GetScriptIdentifier(), script);
							programImporter.SaveAndReimport();
							scriptImporter.AddRemap(identifier, program);
							scriptImporter.SaveAndReimport();
						}
					}
				}
				else
				{
					script = null;
					Debug.LogError("Invalid class. The class must inherit from MonoBehaviour, located in an assembly marked with UdonAsm, and must not be abstract.");
				}
			}
			GUI.color = oldColor;
			return script;
		}

		[InitializeOnLoadMethod]
		private static void Init()
		{
			EditorReplacer.SetMainEditor(typeof(SerializedUdonProgramAsset), typeof(ProgramAssetInspector), false);
		}
	}
}