using Katsudon.Helpers;
using UnityEditor;
using VRC.Udon;

namespace Katsudon.Editor
{
	[CustomEditor(typeof(UdonBehaviour)), CanEditMultipleObjects]
	public class UdonBehaviourDummyEditor : UnityEditor.Editor
	{
		public override void OnInspectorGUI()
		{
			EditorGUILayout.HelpBox("Katsudon editor has not been initialized. Try checking the console for errors.", MessageType.Warning);
		}

		[InitializeOnLoadMethod]
		private static void Init()
		{
			if(EditorReplacer.GetEditor(typeof(UdonBehaviour), false)?.ToString() != "Katsudon.Editor.UdonBehaviourInspector")
			{
				EditorReplacer.SetMainEditor(typeof(UdonBehaviour), typeof(UdonBehaviourDummyEditor), true);
				EditorReplacer.SetMainEditor(typeof(UdonBehaviour), typeof(UdonBehaviourDummyEditor), false);
			}
		}
	}
}