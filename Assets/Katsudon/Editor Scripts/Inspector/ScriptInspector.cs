using Katsudon.Editor.Udon;
using UnityEditor;
using VRC.Udon.ProgramSources;

namespace Katsudon.Editor
{
	[CustomEditor(typeof(MonoScript)), CanEditMultipleObjects]
	public sealed class ScriptInspector : EditorProxyDrawer
	{
		private SerializedUdonProgramAsset program = null;

		protected override void OnInit()
		{
			var fallback = EditorReplacer.GetFallbackEditor(typeof(MonoScript), targets.Length > 1);
			base.CreateEditor(fallback);
			if(targets.Length == 1)
			{
				program = ProgramUtils.GetProgramByScript(targets[0] as MonoScript);
			}
		}

		public override void OnInspectorGUI()
		{
			if(program != null)
			{
				using(new EditorGUI.DisabledScope(true))
				{
					EditorGUILayout.ObjectField("Udon program", program, typeof(SerializedUdonProgramAsset), false);
				}
			}
			base.OnInspectorGUI();
		}
	}
}