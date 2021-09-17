using System;
using System.Collections.Generic;
using System.Reflection;
using Katsudon.Editor.Udon;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.ProgramSources;

namespace Katsudon.Editor
{
	[CustomEditor(typeof(UdonBehaviour)), CanEditMultipleObjects]
	public sealed class UdonBehaviourInspector : EditorProxyDrawer
	{
		private static UnityEditorUtils utils = null;
		private static int dragUpdatedOverID = 0;
		private static double foldoutDestTime;

		private SerializedProperty serializedProgramAssetProp;
		private EditorState state;

		protected override void OnInit()
		{
			serializedProgramAssetProp = serializedObject.FindProperty("serializedProgramAsset");
			InitEditor();
		}

		public override void OnInspectorGUI()
		{
			if(state == EditorState.Proxy)
			{
				base.OnInspectorGUI();
				return;
			}
			if(state == EditorState.CannotEdit)
			{
				GUILayout.Label("Object editing not supported.", EditorStyles.helpBox);
				return;
			}
			if(state == EditorState.CannotEditMulti)
			{
				GUILayout.Label("Multi-object editing not supported.", EditorStyles.helpBox);
				return;
			}
			var enabled = GUI.enabled;
			GUI.enabled = true;
			var indent = EditorGUI.indentLevel;
			EditorGUI.indentLevel = 0;
			base.OnInspectorGUI();
			EditorGUI.indentLevel = indent;
			GUI.enabled = enabled;
		}

		private void InitEditor()
		{
			var targets = base.targets;
			if(serializedProgramAssetProp.hasMultipleDifferentValues)
			{
				bool containsPrograms = false;
				bool allIsPrograms = true;
				for(int i = 0; i < targets.Length; i++)
				{
					if(HasUdonAsmProgram(targets[i]))
					{
						containsPrograms = true;
					}
					else
					{
						allIsPrograms = false;
					}
				}
				state = allIsPrograms ? EditorState.EditDifferent : (containsPrograms ? EditorState.CannotEditMulti : EditorState.Proxy);
			}
			else
			{
				if(serializedProgramAssetProp.objectReferenceValue != null)
				{
					var program = serializedProgramAssetProp.objectReferenceValue as SerializedUdonProgramAsset;
					state = (program == null || !ProgramUtils.HasScriptRecord(program)) ? EditorState.Proxy : EditorState.Edit;
				}
				else state = EditorState.Proxy;
			}

			if(state == EditorState.Proxy)
			{
				var fallback = EditorReplacer.GetFallbackEditor(typeof(UdonBehaviour), targets.Length > 1);
				if(fallback == null)
				{
					state = targets.Length > 1 ? EditorState.CannotEditMulti : EditorState.CannotEdit;
				}
				else base.CreateEditor(fallback);
			}
			else if(state == EditorState.Edit)
			{
				var program = serializedProgramAssetProp.objectReferenceValue as SerializedUdonProgramAsset;
				var behaviours = Array.ConvertAll(targets, v => (UdonBehaviour)v);
				var ubehProxies = Array.ConvertAll(behaviours, ProxyUtils.GetProxyByBehaviour);

				bool hasNoProxy = Array.Exists(ubehProxies, p => p == null);
				var editor = hasNoProxy ? null : CreateEditor(ubehProxies);
				var serializedObject = hasNoProxy ? null : new SerializedObject(ubehProxies);
				SetProxy(CreateMultiProxy(new ProgramEditor[] {
					new ProgramEditor(editor, serializedObject, program, behaviours, ubehProxies)
				}));
			}
			else if(state == EditorState.EditDifferent)
			{
				var groups = new Dictionary<SerializedUdonProgramAsset, List<UdonBehaviour>>();
				for(var i = 0; i < targets.Length; i++)
				{
					var behaviour = (UdonBehaviour)targets[i];
					var program = ProgramUtils.ProgramFieldGetter(behaviour);
					if(!groups.TryGetValue(program, out var list))
					{
						list = new List<UdonBehaviour>();
						groups[program] = list;
					}
					list.Add(behaviour);
				}
				var editors = new ProgramEditor[groups.Count];
				int index = 0;
				foreach(var item in groups)
				{
					var behaviours = item.Value.ToArray();
					var ubehProxies = Array.ConvertAll(behaviours, ProxyUtils.GetProxyByBehaviour);

					bool hasNoProxy = Array.Exists(ubehProxies, p => p == null);
					var editor = hasNoProxy ? null : CreateEditor(ubehProxies);
					var serializedObject = hasNoProxy ? null : new SerializedObject(ubehProxies);
					editors[index] = new ProgramEditor(editor, serializedObject, item.Key, behaviours, ubehProxies);
					index++;
				}
				SetProxy(CreateMultiProxy(editors));
			}
		}

		private EditorProxyCollection CreateMultiProxy(ProgramEditor[] editors)
		{
			var proxies = new IEditorProxy[editors.Length];
			for(int i = 0; i < editors.Length; i++)
			{
				proxies[i] = editors[i].editor == null ? null : CreateProxy(editors[i].editor);
			}
			return new EditorProxyCollection(editors, proxies, this);
		}

		private static bool HasUdonAsmProgram(UnityEngine.Object behaviour)
		{
			var program = ProgramUtils.ProgramFieldGetter((UdonBehaviour)behaviour);
			if(program == null) return false;
			return ProgramUtils.HasScriptRecord(program);
		}

		private static void DrawProxyComponentBackground(Rect position)
		{
			if(Event.current.type == EventType.Repaint)
			{
				position = new Rect(position.x, position.y + 3f, position.width, position.height - 2f);

				Color backgroundColor = GUI.backgroundColor;
				bool enabled = GUI.enabled;
				GUI.enabled = true;
				GUI.backgroundColor = new Color(235f / 255f, 153f / 255f, 1f / 255f, 0.75f);
				position.x = 0f;
				position.width = 3f;
				utils.overrideMargin.Draw(position, false, false, false, false);
				GUI.enabled = enabled;
				GUI.backgroundColor = backgroundColor;
			}
		}

		private static bool InspectorTitlebar(Rect position, bool foldout, ProgramEditor editor)
		{
			int controlID = GUIUtility.GetControlID(utils.titlebarHash, FocusType.Keyboard, position);
			GUIStyle baseStyle = utils.inspectorTitlebar;
			GUIStyle textStyle = utils.inspectorTitlebarText;
			Vector2 buttonIconSize = utils.iconButton.CalcSize(utils.titleSettingsIcon);
			Rect iconPosition = new Rect(position.x + baseStyle.padding.left, position.y + (baseStyle.fixedHeight - 16f) * 0.5f + baseStyle.padding.top, 16f, 16f);
			Rect settingsPosition = new Rect(position.xMax - baseStyle.padding.right - 4f - 16f, position.y + (baseStyle.fixedHeight - buttonIconSize.y) * 0.5f + baseStyle.padding.top, buttonIconSize.x, buttonIconSize.y);
			Rect textPosition = new Rect(iconPosition.xMax + 4f + 4f + 16f, position.y + (baseStyle.fixedHeight - textStyle.fixedHeight) * 0.5f + baseStyle.padding.top, 100f, textStyle.fixedHeight);
			textPosition.xMax = settingsPosition.xMin - 4f;

			Event current = Event.current;
			bool isActive = GUIUtility.hotControl == controlID;
			bool hasKeyboardFocus = GUIUtility.keyboardControl == controlID;
			bool isHover = position.Contains(current.mousePosition);
			if(current.type == EventType.Repaint)
			{
				baseStyle.Draw(position, GUIContent.none, isHover, isActive, foldout, hasKeyboardFocus);
			}
			int enabledState = -1;

			foreach(var behaviour in editor.behaviours)
			{
				int objectEnabled = EditorUtility.GetObjectEnabled(behaviour);
				if(enabledState == -1)
				{
					enabledState = objectEnabled;
				}
				else if(enabledState != objectEnabled)
				{
					enabledState = -2;
					break;
				}
			}
			if(enabledState != -1)
			{
				bool enabled = enabledState != 0;
				EditorGUI.showMixedValue = (enabledState == -2);
				EditorGUI.BeginChangeCheck();
				Color backgroundColor = GUI.backgroundColor;
				bool isAnimated = AnimationMode.IsPropertyAnimated(editor.behaviours[0], "m_Enabled");
				if(isAnimated)
				{
					Color animationColor = AnimationMode.animatedPropertyColor;
					if(utils.InAnimationRecording())
					{
						animationColor = AnimationMode.recordedPropertyColor;
					}
					else if(utils.IsPropertyCandidate(editor.behaviours[0], "m_Enabled"))
					{
						animationColor = AnimationMode.candidatePropertyColor;
					}
					animationColor.a *= GUI.color.a;
					GUI.backgroundColor = animationColor;
				}
				Rect togglePosition = iconPosition;
				togglePosition.x = iconPosition.xMax + 4f;
				enabled = EditorGUI.Toggle(togglePosition, enabled);
				if(isAnimated)
				{
					GUI.backgroundColor = backgroundColor;
				}
				if(EditorGUI.EndChangeCheck())
				{
					Undo.RecordObjects(editor.behaviours, (enabled ? "Enable" : "Disable") + " Component" + ((editor.behaviours.Length <= 1) ? "" : "s"));
					foreach(var behaviour in editor.behaviours)
					{
						EditorUtility.SetObjectEnabled(behaviour, enabled);
					}
				}
				EditorGUI.showMixedValue = false;
				if(togglePosition.Contains(Event.current.mousePosition) && ((current.type == EventType.MouseDown && current.button == 1) || current.type == EventType.ContextClick))
				{
					SerializedObject serializedObject = new SerializedObject(editor.behaviours[0]);
					utils.DoPropertyContextMenu(serializedObject.FindProperty("m_Enabled"), null, null);
					current.Use();
				}
			}
			Rect rectangle = settingsPosition;
			rectangle.x -= 20f;
			textPosition.xMax = utils.DrawEditorHeaderItems(rectangle, editor.proxies, 4f).xMin - 4f;
			if(current.type == EventType.Repaint)
			{
				Texture2D miniThumbnail = AssetPreview.GetMiniThumbnail(editor.proxies[0]);
				GUIStyle.none.Draw(iconPosition, utils.TempContent(miniThumbnail), false, false, false, false);
			}

			bool guiEnabled = GUI.enabled;
			GUI.enabled = true;
			switch(current.GetTypeForControl(controlID))
			{
				case EventType.MouseDown:
					if(settingsPosition.Contains(current.mousePosition))
					{
						DisplayObjectContextMenu(settingsPosition, editor);
						current.Use();
					}
					break;
				case EventType.Repaint:
					bool flag = GUIUtility.hotControl == controlID;
					utils.titlebarFoldout.Draw(new Rect(position.x + utils.titlebarFoldout.margin.left + 1f, position.y + (position.height - 13f) * 0.5f + baseStyle.padding.top, 13f, 13f), flag, flag, foldout, false);

					textStyle.Draw(textPosition, utils.TempContent(ObjectNames.GetInspectorTitle(editor.proxies[0])), isHover, isActive, foldout, hasKeyboardFocus);
					utils.iconButton.Draw(settingsPosition, utils.titleSettingsIcon, controlID, foldout, settingsPosition.Contains(Event.current.mousePosition));
					break;
			}
			GUI.enabled = guiEnabled;
			foldout = DoObjectFoldout(foldout, position, editor, controlID);
			return foldout;
		}

		private static bool DoObjectFoldout(bool foldout, Rect interactionRect, ProgramEditor editor, int id)
		{
			bool enabled = GUI.enabled;
			GUI.enabled = true;
			Event current = Event.current;
			switch(current.GetTypeForControl(id))
			{
				case EventType.MouseDown:
					if(interactionRect.Contains(current.mousePosition))
					{
						if(current.button == 1)
						{
							DisplayObjectContextMenu(new Rect(current.mousePosition, Vector2.zero), editor);
							current.Use();
						}
						else if(current.button == 0 && (Application.platform != 0 || !current.control))
						{
							GUIUtility.hotControl = id;
							GUIUtility.keyboardControl = id;
							var dragAndDropDelay = (DragAndDropDelay)GUIUtility.GetStateObject(typeof(DragAndDropDelay), id);
							dragAndDropDelay.mouseDownPosition = current.mousePosition;
							current.Use();
						}
					}
					break;
				case EventType.ContextClick:
					if(interactionRect.Contains(current.mousePosition))
					{
						DisplayObjectContextMenu(new Rect(current.mousePosition, Vector2.zero), editor);
						current.Use();
					}
					break;
				case EventType.MouseUp:
					if(GUIUtility.hotControl == id)
					{
						GUIUtility.hotControl = 0;
						current.Use();
						if(interactionRect.Contains(current.mousePosition))
						{
							GUI.changed = true;
							foldout = !foldout;
						}
					}
					break;
				case EventType.MouseDrag:
					if(GUIUtility.hotControl == id)
					{
						if(((DragAndDropDelay)GUIUtility.GetStateObject(typeof(DragAndDropDelay), id)).CanStartDrag())
						{
							GUIUtility.hotControl = 0;
							DragAndDrop.PrepareStartDrag();
							DragAndDrop.objectReferences = editor.proxies;
							DragAndDrop.StartDrag((editor.proxies.Length <= 1) ? ObjectNames.GetDragAndDropTitle(editor.proxies[0]) : "<Multiple>");

							// Disable components reordering in inspector
							var dragModeType = typeof(EditorGUI).Assembly.GetType("UnityEditor.EditorDragging").GetNestedType("DraggingMode", BindingFlags.NonPublic);
							DragAndDrop.SetGenericData("InspectorEditorDraggingMode", Activator.CreateInstance(typeof(Nullable<>).MakeGenericType(dragModeType), Enum.ToObject(dragModeType, 0)));
							DragAndDrop.SetGenericData("Katsudon.ComponentDrag", true);
						}
						current.Use();
					}
					break;
				case EventType.DragUpdated:
					if(dragUpdatedOverID == id)
					{
						if(interactionRect.Contains(current.mousePosition))
						{
							if((double)Time.realtimeSinceStartup > foldoutDestTime)
							{
								foldout = true;
								HandleUtility.Repaint();
							}
						}
						else
						{
							dragUpdatedOverID = 0;
						}
					}
					else if(interactionRect.Contains(current.mousePosition))
					{
						dragUpdatedOverID = id;
						foldoutDestTime = (double)Time.realtimeSinceStartup + 0.7;
					}
					if(interactionRect.Contains(current.mousePosition))
					{
						DragAndDrop.visualMode = utils.InspectorWindowDrag(editor.proxies, false);
						Event.current.Use();
					}
					break;
				case EventType.DragPerform:
					if(interactionRect.Contains(current.mousePosition))
					{
						DragAndDrop.visualMode = utils.InspectorWindowDrag(editor.proxies, true);
						DragAndDrop.AcceptDrag();
						Event.current.Use();
					}
					break;
				case EventType.KeyDown:
					if(GUIUtility.keyboardControl == id)
					{
						if(current.keyCode == KeyCode.LeftArrow)
						{
							foldout = false;
							current.Use();
						}
						if(current.keyCode == KeyCode.RightArrow)
						{
							foldout = true;
							current.Use();
						}
					}
					break;
			}
			GUI.enabled = enabled;
			return foldout;
		}

		private static void DisplayObjectContextMenu(Rect rect, ProgramEditor editor)
		{
			var menu = new GenericMenu();
			menu.AddItem(EditorGUIUtility.TrTextContent("Reset"), false, OnResetProxy, editor);
			menu.AddSeparator("");
			menu.AddItem(EditorGUIUtility.TrTextContent("Edit Script"), false, OnEditProxyScript, editor);
			menu.DropDown(rect);
		}

		private static void OnResetProxy(object obj)
		{
			var editor = (ProgramEditor)obj;
			Undo.IncrementCurrentGroup();
			Undo.SetCurrentGroupName("Reset");
			Undo.RecordObjects(editor.proxies, "Reset");
			Undo.RecordObjects(editor.behaviours, "Reset");
			for(int i = 0; i < editor.proxies.Length; i++)
			{
				Unsupported.SmartReset(editor.proxies[i]);
				ProxyUtils.CopyFieldsToBehaviour(editor.proxies[i], editor.behaviours[i]);
			}
		}

		private static void OnEditProxyScript(object obj)
		{
			var editor = (ProgramEditor)obj;
			AssetDatabase.OpenAsset(MonoScript.FromMonoBehaviour(editor.proxies[0]));
		}

		private enum EditorState
		{
			CannotEdit,
			CannotEditMulti,
			Proxy,
			Edit,
			EditDifferent
		}

		private class ProgramEditor : IDisposable
		{
			public UnityEditor.Editor editor;
			public SerializedObject serializedProxies;
			public SerializedUdonProgramAsset program;
			public UdonBehaviour[] behaviours;
			public MonoBehaviour[] proxies;

			public ProgramEditor(UnityEditor.Editor editor, SerializedObject serializedProxies, SerializedUdonProgramAsset program, UdonBehaviour[] behaviours, MonoBehaviour[] proxies)
			{
				this.editor = editor;
				this.serializedProxies = serializedProxies;
				this.program = program;
				this.behaviours = behaviours;
				this.proxies = proxies;

				if(serializedProxies != null)
				{
					Undo.undoRedoPerformed += serializedProxies.Update;
				}
			}

			public void SetSerializedProxes(SerializedObject serializedProxies)
			{
				this.serializedProxies = serializedProxies;
				if(serializedProxies != null)
				{
					Undo.undoRedoPerformed += serializedProxies.Update;
				}
			}

			public void Dispose()
			{
				if(serializedProxies != null)
				{
					Undo.undoRedoPerformed -= serializedProxies.Update;
					serializedProxies.Dispose();
					serializedProxies = null;
				}
				for(int i = 0; i < behaviours.Length; i++)
				{
					if(behaviours[i] == null)
					{
						if(!object.ReferenceEquals(behaviours[i], null))
						{
							if(BehavioursTracker.TryGetContainer(default, out var container))
							{
								container.RemoveBehaviour(behaviours[i]);
							}
						}
						continue;
					}

					// Remove temporary objects
					var scene = behaviours[i].gameObject.scene;
					if(!scene.IsValid())
					{
						if(BehavioursTracker.TryGetContainer(default, out var container))
						{
							container.RemoveBehaviour(behaviours[i]);
						}
					}
				}
			}
		}

		private class EditorProxyCollection : IEditorProxy
		{
			private ProgramEditor[] editors;
			private IEditorProxy[] proxies;
			private UdonBehaviourInspector container;

			public EditorProxyCollection(ProgramEditor[] editors, IEditorProxy[] proxies, UdonBehaviourInspector container)
			{
				this.editors = editors;
				this.proxies = proxies;
				this.container = container;
			}

			public bool HasPreviewGUI()
			{
				return false;
			}

			public string GetInfoString()
			{
				return default;
			}

			public GUIContent GetPreviewTitle()
			{
				return default;
			}

			public void DrawPreview(Rect previewArea)
			{

			}

			public void OnInteractivePreviewGUI(Rect r, GUIStyle background)
			{

			}

			public void OnPreviewGUI(Rect r, GUIStyle background)
			{

			}

			public void OnPreviewSettings()
			{

			}

			public void ReloadPreviewInstances()
			{

			}

			public Texture2D RenderStaticPreview(string assetPath, UnityEngine.Object[] subAssets, int width, int height)
			{
				return null;
			}

			public bool UseDefaultMargins()
			{
				return false;
			}

			public void OnInspectorGUI()
			{
				if(utils == null) utils = new UnityEditorUtils();
				for(var i = 0; i < editors.Length; i++)
				{
					var info = editors[i];
					if(info.editor == null)
					{
						EditorGUILayout.BeginVertical(EditorStyles.inspectorDefaultMargins);
						using(new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("Program Asset", info.program, typeof(SerializedUdonProgramAsset), true);
						if(Array.Exists(info.behaviours, PrefabUtility.IsPartOfPrefabAsset))
						{
							EditorGUILayout.HelpBox("The behavior cannot be edited in the prefab preview mode, you must switch to the prefab edit mode to view and change the behavior", MessageType.Warning);
						}
						else
						{
							if(ProgramAssetInspector.ReplaceMissingClassGUI(info.program) != null)
							{
								var ubehProxies = Array.ConvertAll(info.behaviours, ProxyUtils.GetProxyByBehaviour);
								var editor = CreateEditor(ubehProxies);
								var serializedObject = new SerializedObject(ubehProxies);
								info.proxies = ubehProxies;
								info.editor = editor;
								info.SetSerializedProxes(serializedObject);
								proxies[i] = container.CreateProxy(editor);

								EditorGUIUtility.ExitGUI();
							}
							EditorGUILayout.HelpBox("No MonoBehaviour found for program, please provide a link to the script for normal work", MessageType.Warning);
						}
						EditorGUILayout.EndVertical();
						continue;
					}
					var proxyRect = EditorGUILayout.BeginVertical();
					DrawProxyComponentBackground(proxyRect);

					bool isExpanded = InternalEditorUtility.GetIsInspectorExpanded(info.proxies[0]);
					bool newExpanded = InspectorTitlebar(GUILayoutUtility.GetRect(GUIContent.none, utils.inspectorTitlebar), isExpanded, info);
					if(newExpanded != isExpanded)
					{
						isExpanded = newExpanded;
						InternalEditorUtility.SetIsInspectorExpanded(info.proxies[0], isExpanded);
					}

					if(isExpanded)
					{
						GUIStyle style = (!info.editor.UseDefaultMargins()) ? GUIStyle.none : EditorStyles.inspectorDefaultMargins;
						EditorGUILayout.BeginVertical(style);
						GUI.changed = false;
						try
						{
							using(new EditorGUI.DisabledScope(true))
							{
								EditorGUILayout.ObjectField("Program Asset", info.program, typeof(SerializedUdonProgramAsset), true);
							}
							{
								Networking.SyncType syncType = info.behaviours[0].SyncMethod;
								bool hasDifferentValues = false;
								for(int j = 1; j < info.behaviours.Length; j++)
								{
									if(info.behaviours[j].SyncMethod != syncType)
									{
										hasDifferentValues = true;
										break;
									}
								}
								if(hasDifferentValues) syncType = (Networking.SyncType)Enum.ToObject(typeof(Networking.SyncType), -1);

								EditorGUI.BeginChangeCheck();
								syncType = (Networking.SyncType)EditorGUILayout.EnumPopup("Sync Method", syncType);
								if(EditorGUI.EndChangeCheck())
								{
									var objects = CollectionCache.GetSet<GameObject>();
									for(int j = 0; j < info.behaviours.Length; j++)
									{
										objects.Add(info.behaviours[j].gameObject);
									}
									Undo.IncrementCurrentGroup();
									foreach(var obj in objects)
									{
										var list = obj.GetComponents<UdonBehaviour>();
										Undo.RecordObjects(list, "Sync Method Change");
										list[0].SyncMethod = syncType;
									}
									CollectionCache.Release(objects);
								}
							}

							proxies[i].OnInspectorGUI();
						}
						catch(Exception exception)
						{
							if(exception is ExitGUIException)
							{
								throw;
							}
							Debug.LogException(exception);
						}
						EditorGUILayout.EndVertical();
					}
					EditorGUILayout.EndVertical();
					if(!SOEquals(info.serializedProxies, info.editor.serializedObject))
					{
						Undo.RecordObjects(info.behaviours, "UdonBehaviour Change");
						for(int j = 0; j < info.proxies.Length; j++)
						{
							ProxyUtils.CopyFieldsToBehaviour(info.proxies[j], info.behaviours[j]);
						}
						info.serializedProxies.Update();
					}
				}
			}

			public bool RequiresConstantRepaint()
			{
				for(int i = 0; i < proxies.Length; i++)
				{
					if(proxies[i] != null && proxies[i].UseDefaultMargins()) return true;
				}
				return false;
			}

			public void OnHeaderGUI()
			{

			}

			public void OnSceneGUI()
			{
				for(int i = 0; i < proxies.Length; i++)
				{
					proxies[i]?.OnSceneGUI();
				}
			}

			public bool HasFrameBounds()
			{
				return false;
			}

			public Bounds OnGetFrameBounds()
			{
				return default;
			}

			public bool ShouldHideOpenButton()
			{
				return true;
			}

			public void Dispose()
			{
				for(int i = 0; i < proxies.Length; i++)
				{
					proxies[i]?.Dispose();
				}
				for(int i = 0; i < editors.Length; i++)
				{
					editors[i].Dispose();
				}
			}

			private static bool SOEquals(SerializedObject a, SerializedObject b)
			{
				var isFirst = true;
				var pa = a.GetIterator();
				var pb = b.GetIterator();
				while(pa.Next(isFirst) && pb.Next(isFirst))
				{
					if(!SerializedProperty.DataEquals(pa, pb)) return false;
					isFirst = false;
				}
				return true;
			}
		}

		private class DragAndDropDelay
		{
			public Vector2 mouseDownPosition;

			public bool CanStartDrag()
			{
				return Vector2.Distance(mouseDownPosition, Event.current.mousePosition) > 6f;
			}
		}

		private class UnityEditorUtils
		{
			private const BindingFlags INTERNAL_BIND = BindingFlags.NonPublic | BindingFlags.Static;

			public readonly int titlebarHash;

			public GUIStyle overrideMargin => _overrideMargin();
			public GUIStyle inspectorTitlebar => _inspectorTitlebar();
			public GUIStyle titlebarFoldout => _titlebarFoldout();
			public GUIStyle inspectorTitlebarText => _inspectorTitlebarText();
			public GUIStyle iconButton => _iconButton();

			public GUIContent titleSettingsIcon => _titleSettingsIcon();

			public readonly Func<bool> InAnimationRecording;
			public readonly Func<UnityEngine.Object, string, bool> IsPropertyCandidate;
			public readonly Func<UnityEngine.Object[], bool, DragAndDropVisualMode> InspectorWindowDrag;
			public readonly Action<SerializedProperty, SerializedProperty, GenericMenu> DoPropertyContextMenu;
			public readonly Func<Rect, UnityEngine.Object[], float, Rect> DrawEditorHeaderItems;

			private Func<GUIStyle> _overrideMargin;
			private Func<GUIStyle> _inspectorTitlebar;
			private Func<GUIStyle> _titlebarFoldout;
			private Func<GUIStyle> _inspectorTitlebarText;
			private Func<GUIStyle> _iconButton;

			private Func<GUIContent> _titleSettingsIcon;

			private Func<Texture, GUIContent> _tempContentTexture;
			private Func<string, GUIContent> _tempContentText;

			public UnityEditorUtils()
			{
				titlebarHash = (int)typeof(EditorGUI).GetField("s_TitlebarHash", INTERNAL_BIND).GetValue(null);

				_overrideMargin = CreatePropertyGetter<GUIStyle>(typeof(EditorStyles), "overrideMargin");
				_inspectorTitlebar = CreatePropertyGetter<GUIStyle>(typeof(EditorStyles), "inspectorTitlebar");
				_titlebarFoldout = CreatePropertyGetter<GUIStyle>(typeof(EditorStyles), "titlebarFoldout");
				_inspectorTitlebarText = CreatePropertyGetter<GUIStyle>(typeof(EditorStyles), "inspectorTitlebarText");
				_iconButton = CreatePropertyGetter<GUIStyle>(typeof(EditorStyles), "iconButton");

				_titleSettingsIcon = CreatePropertyGetter<GUIContent>(typeof(EditorGUI).GetNestedType("GUIContents", INTERNAL_BIND), "titleSettingsIcon");

				_tempContentTexture = CreateFunc<Texture, GUIContent>(typeof(EditorGUIUtility), "TempContent");
				_tempContentText = CreateFunc<string, GUIContent>(typeof(EditorGUIUtility), "TempContent");

				InAnimationRecording = CreateFunc<bool>(typeof(AnimationMode), "InAnimationRecording");
				IsPropertyCandidate = CreateFunc<UnityEngine.Object, string, bool>(typeof(AnimationMode), "IsPropertyCandidate");
				InspectorWindowDrag = CreateFunc<UnityEngine.Object[], bool, DragAndDropVisualMode>(typeof(InternalEditorUtility), "InspectorWindowDrag");
				DoPropertyContextMenu = CreateAction<SerializedProperty, SerializedProperty, GenericMenu>(typeof(EditorGUI), "DoPropertyContextMenu");
				DrawEditorHeaderItems = CreateFunc<Rect, UnityEngine.Object[], float, Rect>(typeof(EditorGUIUtility), "DrawEditorHeaderItems");
			}

			public GUIContent TempContent(Texture texture)
			{
				return _tempContentTexture(texture);
			}

			public GUIContent TempContent(string text)
			{
				return _tempContentText(text);
			}

			private static Func<TOut> CreatePropertyGetter<TOut>(Type type, string name)
			{
				return (Func<TOut>)(object)Delegate.CreateDelegate(typeof(Func<TOut>), type.GetProperty(name, INTERNAL_BIND).GetGetMethod(true));
			}

			private static Func<TOut> CreateFunc<TOut>(Type type, string name)
			{
				return (Func<TOut>)(object)Delegate.CreateDelegate(typeof(Func<TOut>), type.GetMethod(name, INTERNAL_BIND, null, Type.EmptyTypes, null));
			}

			private static Func<TIn, TOut> CreateFunc<TIn, TOut>(Type type, string name)
			{
				return (Func<TIn, TOut>)(object)Delegate.CreateDelegate(typeof(Func<TIn, TOut>), type.GetMethod(name, INTERNAL_BIND, null, new Type[] { typeof(TIn) }, null));
			}

			private static Func<TIn0, TIn1, TOut> CreateFunc<TIn0, TIn1, TOut>(Type type, string name)
			{
				return (Func<TIn0, TIn1, TOut>)(object)Delegate.CreateDelegate(typeof(Func<TIn0, TIn1, TOut>), type.GetMethod(name, INTERNAL_BIND, null, new Type[] { typeof(TIn0), typeof(TIn1) }, null));
			}

			private static Func<TIn0, TIn1, TIn2, TOut> CreateFunc<TIn0, TIn1, TIn2, TOut>(Type type, string name)
			{
				return (Func<TIn0, TIn1, TIn2, TOut>)(object)Delegate.CreateDelegate(typeof(Func<TIn0, TIn1, TIn2, TOut>), type.GetMethod(name, INTERNAL_BIND, null, new Type[] { typeof(TIn0), typeof(TIn1), typeof(TIn2) }, null));
			}

			private static Action<TIn0, TIn1, TIn2> CreateAction<TIn0, TIn1, TIn2>(Type type, string name)
			{
				return (Action<TIn0, TIn1, TIn2>)(object)Delegate.CreateDelegate(typeof(Action<TIn0, TIn1, TIn2>), type.GetMethod(name, INTERNAL_BIND, null, new Type[] { typeof(TIn0), typeof(TIn1), typeof(TIn2) }, null));
			}
		}
	}
}