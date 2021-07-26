using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace Katsudon.Editor
{
	public class UMachineStateView : EditorWindow
	{
		private UdonBehaviour targetObject = null;
		private FieldInfo programField = typeof(UdonBehaviour).GetField("_program", BindingFlags.NonPublic | BindingFlags.Instance);
		private Vector2 scroll = Vector2.zero;
		private List<(uint address, IStrongBox strongBoxedObject, Type objectType)> heapDump = new List<(uint address, IStrongBox strongBoxedObject, Type objectType)>();

		void OnGUI()
		{
			targetObject = (UdonBehaviour)EditorGUILayout.ObjectField("Target", targetObject, typeof(UdonBehaviour), true);
			UdonBehaviour ubeh = targetObject;
			if(ubeh == null)
			{
				if(Selection.activeGameObject != null)
				{
					ubeh = Selection.activeGameObject.GetComponent<UdonBehaviour>();
				}
			}
			if(ubeh == null)
			{
				GUILayout.Label("Select a behavior or set as a target");
				return;
			}
			IUdonProgram program = (IUdonProgram)programField.GetValue(ubeh);
			if(program == null)
			{
				GUILayout.Label("The target does not have an initialized program");
				return;
			}
			scroll = EditorGUILayout.BeginScrollView(scroll);

			GUILayout.Label("Heap:", EditorStyles.boldLabel);

			heapDump.Clear();
			program.Heap.DumpHeapObjects(heapDump);
			heapDump.Sort((a, b) => a.address.CompareTo(b.address));

			GUILayout.BeginHorizontal();
			GUILayout.BeginVertical();
			GUILayout.Label("Variable", EditorStyles.boldLabel);
			var symbols = program.SymbolTable;
			for(var i = 0; i < heapDump.Count; i++)
			{
				var value = heapDump[i];
				GUILayout.Label(string.Format("(0x{0:X8}) {1}", value.address, symbols.HasSymbolForAddress(value.address) ? symbols.GetSymbolFromAddress(value.address) : "[Unknown]"));
			}
			GUILayout.EndVertical();
			GUILayout.BeginVertical();
			GUILayout.Label("Value", EditorStyles.boldLabel);
			for(var i = 0; i < heapDump.Count; i++)
			{
				var value = heapDump[i];
				GUILayout.Label(string.Format("{0}", value.strongBoxedObject.Value));
			}
			GUILayout.EndVertical();
			GUILayout.BeginVertical();
			GUILayout.Label("Type", EditorStyles.boldLabel);
			for(var i = 0; i < heapDump.Count; i++)
			{
				var value = heapDump[i];
				GUILayout.Label(value.objectType.ToString());
			}
			GUILayout.EndVertical();
			GUILayout.EndHorizontal();
			EditorGUILayout.EndScrollView();
		}

		[MenuItem("Tools/Udon Machine State")]
		public static void ShowWindow()
		{
			var editorWindow = (ScriptableObject.CreateInstance(typeof(UMachineStateView)) as UMachineStateView);
			editorWindow.titleContent = new GUIContent("Udon Machine State");
			editorWindow.ShowUtility();
		}
	}
}