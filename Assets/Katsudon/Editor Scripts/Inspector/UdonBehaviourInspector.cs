using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Katsudon.Editor.Udon;
using Katsudon.Helpers;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;
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

		private bool isPrefabEditor = false;
		private UdonBehaviour[] prefabBehaviours;

		private OverriddenGUIContainer guiContainer;

		protected override void OnInit()
		{
			serializedProgramAssetProp = serializedObject.FindProperty("serializedProgramAsset");
			InitEditor();
		}

		protected override void OnDisable()
		{
			base.OnDisable();
			if(isPrefabEditor)
			{
				for(int i = 0; i < prefabBehaviours.Length; i++)
				{
					BehavioursTracker.ReleasePrefabBehaviour(prefabBehaviours[i]);
				}
			}
		}

		public override VisualElement CreateInspectorGUI()
		{
			if(state == EditorState.ProxyEditor || state == EditorState.ProxyEditorMulti)
			{
				guiContainer = new OverriddenGUIContainer(this, OnInspectorGUI);
				return guiContainer;
			}
			guiContainer = null;
			return new IMGUIContainer(OnInspectorGUI);
		}

		public override void OnInspectorGUI()
		{
			if(state == EditorState.DefaultEditor)
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

			guiContainer.footer.style.marginTop = -3f;
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
				state = allIsPrograms ? EditorState.ProxyEditorMulti : (containsPrograms ? EditorState.CannotEditMulti : EditorState.DefaultEditor);
			}
			else
			{
				if(serializedProgramAssetProp.objectReferenceValue != null)
				{
					var program = serializedProgramAssetProp.objectReferenceValue as SerializedUdonProgramAsset;
					state = (program == null || !ProgramUtils.HasScriptRecord(program)) ? EditorState.DefaultEditor : EditorState.ProxyEditor;
				}
				else state = EditorState.DefaultEditor;
			}

			if(state == EditorState.DefaultEditor)
			{
				var fallback = EditorReplacer.GetFallbackEditor(typeof(UdonBehaviour), new Type[] { typeof(UdonBehaviourDummyEditor) }, targets.Length > 1);
				if(fallback == null)
				{
					state = targets.Length > 1 ? EditorState.CannotEditMulti : EditorState.CannotEdit;
				}
				else base.CreateEditor(fallback);
			}
			else if(state == EditorState.ProxyEditor)
			{
				var program = serializedProgramAssetProp.objectReferenceValue as SerializedUdonProgramAsset;
				var behaviours = Array.ConvertAll(targets, v => (UdonBehaviour)v);
				var ubehProxies = Array.ConvertAll(behaviours, ProxyUtils.GetProxyByBehaviour);

				bool hasNoProxy = Array.Exists(ubehProxies, p => p == null);
				var editor = hasNoProxy ? null : CreateEditor(ubehProxies);
				if(editor != null) editor.hideFlags = HideFlags.HideAndDontSave;
				SetProxy(CreateMultiProxy(new ProgramEditor[] {
					new ProgramEditor(editor, program, behaviours, ubehProxies)
				}));
			}
			else if(state == EditorState.ProxyEditorMulti)
			{
				var groups = new Dictionary<SerializedUdonProgramAsset, List<UdonBehaviour>>();
				for(var i = 0; i < targets.Length; i++)
				{
					var behaviour = (UdonBehaviour)targets[i];
					var program = ProgramUtils.ProgramFieldGetter(behaviour);
					if(!groups.TryGetValue(program, out var list))
					{
						list = CollectionCache.GetList<UdonBehaviour>();
						groups[program] = list;
					}
					list.Add(behaviour);
				}
				var editors = new ProgramEditor[groups.Count];
				int index = 0;
				foreach(var item in groups)
				{
					var behaviours = item.Value.ToArray();
					CollectionCache.Release(item.Value);
					var ubehProxies = Array.ConvertAll(behaviours, ProxyUtils.GetProxyByBehaviour);

					bool hasNoProxy = Array.Exists(ubehProxies, p => p == null);
					var editor = hasNoProxy ? null : CreateEditor(ubehProxies);
					if(editor != null) editor.hideFlags = HideFlags.HideAndDontSave;
					editors[index] = new ProgramEditor(editor, item.Key, behaviours, ubehProxies);
					index++;
				}
				SetProxy(CreateMultiProxy(editors));
			}

			if(state == EditorState.ProxyEditor || state == EditorState.ProxyEditorMulti)
			{
				isPrefabEditor = PrefabUtility.IsPartOfPrefabAsset(targets[0]);
				if(isPrefabEditor) prefabBehaviours = Array.ConvertAll(targets, t => (UdonBehaviour)t);
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

		private static void DrawProxyComponentBackground(Rect position, float width = 3f, float offset = 0f)
		{
			if(Event.current.type == EventType.Repaint)
			{
				position.x = offset;
				position.y += 3f;
				position.width = width;
				position.height -= 2f;

				Color backgroundColor = GUI.backgroundColor;
				bool enabled = GUI.enabled;
				GUI.enabled = true;
				GUI.backgroundColor = new Color(235f / 255f, 153f / 255f, 1f / 255f, 0.75f);
				utils.overrideMargin.Draw(position, false, false, false, false);
				GUI.enabled = enabled;
				GUI.backgroundColor = backgroundColor;
			}
		}

		private static bool ProxyInspectorTitlebar(Rect position, bool foldout, ProgramEditor editor, bool drawPrefabOverride)
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

			if(drawPrefabOverride)
			{
				utils.DrawOverrideBackground(position, true);
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
				if(togglePosition.Contains(current.mousePosition) && ((current.type == EventType.MouseDown && current.button == 1) || current.type == EventType.ContextClick))
				{
					SerializedObject serializedObject = new SerializedObject(editor.behaviours[0]);
					utils.DoPropertyContextMenu(serializedObject.FindProperty("m_Enabled"), null, null);
					current.Use();
				}
			}

			if(current.type == EventType.Repaint)
			{
				Texture2D miniThumbnail = AssetPreview.GetMiniThumbnail(editor.proxies[0]);
				GUIStyle.none.Draw(iconPosition, utils.TempContent(miniThumbnail), false, false, false, false);
				if(drawPrefabOverride)
				{
					GUIStyle.none.Draw(iconPosition, utils.TempContent(utils.prefabOverlayAddedIcon), false, false, false, false);
				}
			}

			Rect udonRectangle = settingsPosition;
			udonRectangle.x -= 20f;
			textPosition.xMax = utils.DrawEditorHeaderItems(udonRectangle, editor.proxies, 4f).xMin - 4f;

			udonRectangle = new Rect(textPosition.xMax - 96f, position.y, 96f, position.height);
			textPosition.xMax -= 100f;

			bool guiEnabled = GUI.enabled;
			GUI.enabled = true;
			switch(current.GetTypeForControl(controlID))
			{
				case EventType.MouseDown:
					if(settingsPosition.Contains(current.mousePosition))
					{
						ProxyContextMenu.Display(settingsPosition, editor);
						current.Use();
					}
					break;
				case EventType.Repaint:
					utils.titlebarFoldout.Draw(new Rect(position.x + utils.titlebarFoldout.margin.left + 1f, position.y + (position.height - 13f) * 0.5f + baseStyle.padding.top, 13f, 13f), isActive, isActive, foldout, false);

					textStyle.Draw(textPosition, utils.TempContent(ObjectNames.GetInspectorTitle(editor.proxies[0])), isHover, isActive, foldout, hasKeyboardFocus);
					utils.iconButton.Draw(settingsPosition, utils.titleSettingsIcon, controlID, foldout, settingsPosition.Contains(current.mousePosition));
					break;
			}
			GUI.enabled = guiEnabled;
			DoUdonBehaviourArea(udonRectangle, editor, foldout, GUIUtility.GetControlID(utils.titlebarHash, FocusType.Passive, position));
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
							ProxyContextMenu.Display(new Rect(current.mousePosition, Vector2.zero), editor);
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
						ProxyContextMenu.Display(new Rect(current.mousePosition, Vector2.zero), editor);
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
							DragAndDrop.SetGenericData("InspectorEditorDraggingMode", utils.InspectorEditorDraggingMode);
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

		private static void DoUdonBehaviourArea(Rect rect, ProgramEditor editor, bool foldout, int id)
		{
			GUIStyle baseStyle = utils.inspectorTitlebar;
			GUIStyle textStyle = utils.inspectorTitlebarText;

			var current = Event.current;
			bool isHover = rect.Contains(current.mousePosition);
			bool isActive = GUIUtility.hotControl == id;
			switch(current.GetTypeForControl(id))
			{
				case EventType.MouseDown:
					if(isHover)
					{
						if(current.button == 0 && (Application.platform != 0 || !current.control))
						{
							GUIUtility.hotControl = id;
							GUIUtility.keyboardControl = id;
							var dragAndDropDelay = GUIUtility.GetStateObject(utils.DragAndDropDelay, id);
							utils.dragAndDropDelayField.SetValue(dragAndDropDelay, current.mousePosition);
							current.Use();
						}
					}
					break;
				case EventType.MouseUp:
					if(GUIUtility.hotControl == id)
					{
						GUIUtility.hotControl = 0;
						current.Use();
					}
					break;
				case EventType.MouseDrag:
					if(GUIUtility.hotControl == id)
					{
						if((bool)utils.dragAndDropDelayMethod.Invoke(GUIUtility.GetStateObject(utils.DragAndDropDelay, id), new object[0]))
						{
							GUIUtility.hotControl = 0;
							DragAndDrop.PrepareStartDrag();
							DragAndDrop.objectReferences = editor.behaviours;
							DragAndDrop.StartDrag((editor.behaviours.Length <= 1) ? ObjectNames.GetDragAndDropTitle(editor.behaviours[0]) : "<Multiple>");
						}
						current.Use();
					}
					break;
				case EventType.DragUpdated:
					if(dragUpdatedOverID == id)
					{
						if(!isHover)
						{
							dragUpdatedOverID = 0;
						}
					}
					else if(isHover)
					{
						dragUpdatedOverID = id;
						foldoutDestTime = (double)Time.realtimeSinceStartup + 0.7;
					}
					if(isHover)
					{
						DragAndDrop.visualMode = utils.InspectorWindowDrag(editor.behaviours, false);
						Event.current.Use();
					}
					break;
				case EventType.DragPerform:
					if(isHover)
					{
						DragAndDrop.visualMode = utils.InspectorWindowDrag(editor.behaviours, true);
						DragAndDrop.AcceptDrag();
						Event.current.Use();
					}
					break;
				case EventType.Repaint:
					Color backgroundColor = GUI.backgroundColor;
					GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f);
					baseStyle.Draw(rect, GUIContent.none, isHover, isActive, foldout, false);
					GUI.backgroundColor = backgroundColor;

					rect.x += 4f;
					textStyle.Draw(rect, EditorGUIUtility.TrTextContent("UdonBehaviour", "UdonBehaviour Drag&Drop"), isHover, isActive, foldout, false);
					break;
			}
		}

		[InitializeOnLoadMethod]
		private static void Init()
		{
			EditorReplacer.SetMainEditor(typeof(UdonBehaviour), typeof(UdonBehaviourInspector), true);
			EditorReplacer.SetMainEditor(typeof(UdonBehaviour), typeof(UdonBehaviourInspector), false);
		}

		private enum EditorState
		{
			CannotEdit,
			CannotEditMulti,
			DefaultEditor,
			ProxyEditor,
			ProxyEditorMulti
		}

		private class OverriddenGUIContainer : IMGUIContainer
		{
			public IMGUIContainer footer;

			private UnityEditor.Editor target;
			private ContainerInfo container = default;

			public OverriddenGUIContainer(UnityEditor.Editor target, Action onGUIHandler) : base(onGUIHandler)
			{
				this.target = target;
				style.overflow = Overflow.Visible;
				AddToClassList(InspectorElement.iMGUIContainerUssClassName);
			}

			public override void HandleEvent(EventBase evt)
			{
				base.HandleEvent(evt);
				if(evt is AttachToPanelEvent)
				{
					// Hiding UdonBehaviour inspector title
					var hierarchy = parent.parent.hierarchy;
					for(int i = 0; i < hierarchy.childCount; i++)
					{
						if(hierarchy[i] is IMGUIContainer container)
						{
							if(container.name.EndsWith("Header"))
							{
								this.container.Release();
								this.container = new ContainerInfo(container);
								this.container.Use();
							}
							if(container.name.EndsWith("Footer"))
							{
								footer = container;
							}
						}
					}
				}
				if(evt is DetachFromPanelEvent)
				{
					this.container.Release();
					this.container = default;
				}
			}

			private struct ContainerInfo
			{
				private IMGUIContainer container;
				private bool isEnabled;
				private bool isFocusable;
				private StyleLength marginTop;
				private StyleEnum<Visibility> visibility;

				public ContainerInfo(IMGUIContainer container)
				{
					this.container = container;
					isEnabled = container.enabledSelf;
					isFocusable = container.focusable;
					marginTop = container.style.marginTop;
					visibility = container.style.visibility;
				}

				public void Use()
				{
					container.SetEnabled(false);
					container.focusable = false;
					container.style.marginTop = -22; // looks bad.. but only works like this
					container.style.visibility = Visibility.Hidden;
				}

				public void Release()
				{
					if(container == null) return;
					container.SetEnabled(isEnabled);
					container.focusable = isFocusable;
					container.style.marginTop = marginTop;
					container.style.visibility = visibility;
					container = null;
				}
			}
		}

		private class ProgramEditor
		{
			public UnityEditor.Editor editor;
			public SerializedUdonProgramAsset program;
			public UdonBehaviour[] behaviours;
			public MonoBehaviour[] proxies;

			public ProgramEditor(UnityEditor.Editor editor, SerializedUdonProgramAsset program, UdonBehaviour[] behaviours, MonoBehaviour[] proxies)
			{
				this.editor = editor;
				this.program = program;
				this.behaviours = behaviours;
				this.proxies = proxies;
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
						if(ProgramAssetInspector.ReplaceMissingClassGUI(info.program) != null)
						{
							var ubehProxies = Array.ConvertAll(info.behaviours, ProxyUtils.GetProxyByBehaviour);
							if(ubehProxies[0] != null)
							{
								var editor = CreateEditor(ubehProxies);
								if(editor != null) editor.hideFlags = HideFlags.HideAndDontSave;
								info.proxies = ubehProxies;
								info.editor = editor;
								proxies[i] = container.CreateProxy(editor);

								EditorGUIUtility.ExitGUI();
							}
						}
						EditorGUILayout.HelpBox("No MonoBehaviour found for program, provide a link to the script for normal work", MessageType.Warning);
						EditorGUILayout.EndVertical();
						continue;
					}

					bool isPrefabOverride = info.editor != null && info.behaviours.Length == 1 && utils.ShouldDrawOverrideBackground(info.behaviours, Event.current, info.behaviours[0]);
					var overrideRect = EditorGUILayout.BeginVertical();
					float overrideOffset = 0f;
					float overrideWidth = 3f;
					if(isPrefabOverride)
					{
						utils.DrawOverrideBackground(new Rect(overrideRect.x, overrideRect.y + 3f, overrideRect.width, overrideRect.height - 2f), true);
						overrideOffset = 2f;
						overrideWidth = 2f;
					}
					DrawProxyComponentBackground(overrideRect, overrideWidth, overrideOffset);

					bool isExpanded = InternalEditorUtility.GetIsInspectorExpanded(info.proxies[0]);
					bool newExpanded = ProxyInspectorTitlebar(GUILayoutUtility.GetRect(GUIContent.none, utils.inspectorTitlebar), isExpanded, info, isPrefabOverride);
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

							EditorGUILayout.BeginVertical(new GUIStyle(EditorStyles.helpBox));
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
									if(syncType == Networking.SyncType.None)
									{
										info.editor.serializedObject.ApplyModifiedProperties();
										Undo.IncrementCurrentGroup();
										Undo.RecordObjects(info.behaviours, "Sync Method Change");
										for(int j = 0; j < info.behaviours.Length; j++)
										{
											info.behaviours[j].SyncMethod = syncType;
										}
										info.editor.serializedObject.Update();
									}
									else
									{
										foreach(var ei in editors)
										{
											if(ei.editor != null)
											{
												ei.editor.serializedObject.ApplyModifiedProperties();
											}
										}
										var objects = CollectionCache.GetSet<GameObject>();
										for(int j = 0; j < info.behaviours.Length; j++)
										{
											objects.Add(info.behaviours[j].gameObject);
										}
										Undo.IncrementCurrentGroup();
										for(int j = 0; j < info.behaviours.Length; j++)
										{
											var beh = info.behaviours[j];
											if(objects.Remove(beh.gameObject))
											{
												Undo.RecordObjects(beh.gameObject.GetComponents<UdonBehaviour>(), "Sync Method Change");
												beh.SyncMethod = syncType;
											}
										}
										CollectionCache.Release(objects);
										foreach(var ei in editors)
										{
											if(ei.editor != null)
											{
												ei.editor.serializedObject.Update();
											}
										}
									}
								}

								switch(syncType)
								{
									case VRC.SDKBase.Networking.SyncType.None:
										EditorGUILayout.LabelField("Replication will be disabled.", EditorStyles.wordWrappedLabel);
										break;
									case VRC.SDKBase.Networking.SyncType.Continuous:
										EditorGUILayout.LabelField("Continuous replication is intended for frequently-updated variables of small size, and will be tweened. Ideal for physics objects and objects that must be in sync with players.", EditorStyles.wordWrappedLabel);
										break;
									case VRC.SDKBase.Networking.SyncType.Manual:
										EditorGUILayout.LabelField("Manual replication is intended for infrequently-updated variables of small or large size, and will not be tweened. Ideal for infrequently modified abstract data.", EditorStyles.wordWrappedLabel);
										break;
									default:
										EditorGUILayout.LabelField("Unknown method", EditorStyles.wordWrappedLabel);
										break;
								}
							}
							EditorGUILayout.EndVertical();

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
				}
			}

			public bool RequiresConstantRepaint()
			{
				for(int i = 0; i < proxies.Length; i++)
				{
					if(proxies[i] != null && proxies[i].RequiresConstantRepaint()) return true;
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

		private static class ProxyContextMenu
		{
			public static void Display(Rect rect, ProgramEditor editor)
			{
				bool isSingle = editor.behaviours.Length == 1;
				bool isFirst = false;
				bool isLast = false;
				if(isSingle)
				{
					var list = CollectionCache.GetList<Component>();
					editor.behaviours[0].gameObject.GetComponents(list);
					int index = list.IndexOf(editor.behaviours[0]);
					isFirst = index <= 1;
					isLast = true;
					for(int i = index + 1; i < list.Count; i++)
					{
						if((list[i].hideFlags & HideFlags.HideInInspector) == 0)
						{
							isLast = false;
							break;
						}
					}
					CollectionCache.Release(list);
				}

				var menu = new GenericMenu();

				menu.AddItem(EditorGUIUtility.TrTextContent("Reset"), false, OnReset, editor);
				if(isSingle) AddPrefabMenuItems(menu, editor.behaviours[0]);
				menu.AddItem(EditorGUIUtility.TrTextContent("Pull Values From Behaviour",
					"Pulls changes from behavior if they haven't been updated for some reason."), false, OnPullValues, editor);
				menu.AddSeparator("");
				menu.AddItem(EditorGUIUtility.TrTextContent("Remove Component"), false, OnRemove, editor);
				if(isSingle && !isFirst) menu.AddItem(EditorGUIUtility.TrTextContent("Move Up"), false, OnMoveUp, editor);
				else menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Move Up"));
				if(isSingle && !isLast) menu.AddItem(EditorGUIUtility.TrTextContent("Move Down"), false, OnMoveDown, editor);
				else menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Move Down"));
				if(isSingle) menu.AddItem(EditorGUIUtility.TrTextContent("Copy Component"), false, OnCopy, editor);
				else menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Copy Component"));
				if(isSingle) menu.AddItem(EditorGUIUtility.TrTextContent("Paste Component As New"), false, OnPasteComponentAsNew, editor);
				else menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Paste Component As New"));
				if(isSingle) menu.AddItem(EditorGUIUtility.TrTextContent("Paste Component Values"), false, OnPasteComponentValues, editor);
				else menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Paste Component Values"));
				menu.AddSeparator("");
				if(isSingle) menu.AddItem(EditorGUIUtility.TrTextContent("Find References In Scene"), false, OnFindProxyReferences, editor);
				else menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Find References In Scene"));
				if(isSingle) menu.AddItem(EditorGUIUtility.TrTextContent("Find UdonBehaviour References In Scene"), false, OnFindBehaviourReferences, editor);
				else menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Find UdonBehaviour References In Scene"));
				menu.AddItem(EditorGUIUtility.TrTextContent("Edit Script"), false, OnEditScript, editor);

				if(editor.proxies.Length == 1)
				{
					var methods = editor.proxies[0].GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
					Dictionary<string, ContextMenuItem> additionalItems = null;
					for(int i = 0; i < methods.Length; i++)
					{
						var attrib = methods[i].GetCustomAttribute<ContextMenu>();
						if(attrib != null)
						{
							if(additionalItems == null) additionalItems = CollectionCache.GetDictionary<string, ContextMenuItem>();
							if(!additionalItems.TryGetValue(attrib.menuItem, out var item))
							{
								item = new ContextMenuItem(attrib.menuItem, editor.proxies[0]);
								additionalItems[attrib.menuItem] = item;
							}
							if(attrib.validate) item.condition = methods[i];
							else
							{
								item.action = methods[i];
								item.order = attrib.priority;
							}
						}
					}
					if(additionalItems != null)
					{
						bool isFirstItem = true;
						var list = CollectionCache.GetList(additionalItems.Values);
						CollectionCache.Release(additionalItems);
						foreach(var item in list)
						{
							if(item.CanBeAdded())
							{
								if(isFirstItem)
								{
									isFirstItem = false;
									menu.AddSeparator("");
								}
								item.AddToMenu(menu);
							}
						}
						CollectionCache.Release(list);
					}
				}

				menu.DropDown(rect);
			}

			private static void OnReset(object obj)
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

			private static void OnPullValues(object obj)
			{
				var editor = (ProgramEditor)obj;
				for(int i = 0; i < editor.proxies.Length; i++)
				{
					Unsupported.SmartReset(editor.proxies[i]);
					ProxyUtils.CopyFieldsToProxy(editor.behaviours[i], editor.proxies[i]);
				}
			}

			private static void OnRemove(object obj)
			{
				var editor = (ProgramEditor)obj;
				Undo.IncrementCurrentGroup();
				int group = Undo.GetCurrentGroup();
				var behaviours = editor.behaviours;
				var proxies = editor.proxies;
				for(int i = behaviours.Length - 1; i >= 0; i--)
				{
					BehavioursTracker.UnRegisterPair(behaviours[i]);
					Undo.DestroyObjectImmediate(behaviours[i]);
				}
				Undo.CollapseUndoOperations(group);
				utils.ForceRebuildInspectors();
			}

			private static void OnMoveUp(object obj)
			{
				var editor = (ProgramEditor)obj;
				ComponentUtility.MoveComponentUp(editor.behaviours[0]);
			}

			private static void OnMoveDown(object obj)
			{
				var editor = (ProgramEditor)obj;
				ComponentUtility.MoveComponentDown(editor.behaviours[0]);
			}

			private static void OnCopy(object obj)
			{
				var editor = (ProgramEditor)obj;
				ComponentUtility.CopyComponent(editor.behaviours[0]);
			}

			private static void OnPasteComponentAsNew(object obj)
			{
				var editor = (ProgramEditor)obj;
				ComponentUtility.PasteComponentAsNew(editor.behaviours[0].gameObject);
			}

			private static void OnPasteComponentValues(object obj)
			{
				var editor = (ProgramEditor)obj;
				ComponentUtility.PasteComponentValues(editor.behaviours[0]);
				int group = Undo.GetCurrentGroup();
				Undo.RecordObject(editor.proxies[0], "Paste Component Values");
				ProxyUtils.CopyFieldsToProxy(editor.behaviours[0], editor.proxies[0]);
				Undo.CollapseUndoOperations(group);
			}

			private static void OnFindProxyReferences(object obj)
			{
				var editor = (ProgramEditor)obj;
				utils.FindComponentReferencesOnScene(new MenuCommand(editor.proxies[0], 0));
			}

			private static void OnFindBehaviourReferences(object obj)
			{
				var editor = (ProgramEditor)obj;
				utils.FindComponentReferencesOnScene(new MenuCommand(editor.behaviours[0], 0));
			}

			private static void OnEditScript(object obj)
			{
				var editor = (ProgramEditor)obj;
				AssetDatabase.OpenAsset(MonoScript.FromMonoBehaviour(editor.proxies[0]));
			}

			private static void AddPrefabMenuItems(GenericMenu menu, UdonBehaviour targetComponent)
			{
				if(PrefabUtility.IsDisconnectedFromPrefabAsset(targetComponent.gameObject)) return;
				if(PrefabUtility.GetCorrespondingObjectFromSource(targetComponent.gameObject) == null) return;
				utils.InitPrefabUtils();
				if(PrefabUtility.GetCorrespondingObjectFromSource(targetComponent) == null && targetComponent != null)
				{
					GameObject instanceGo = targetComponent.gameObject;
					utils.HandleApplyRevertMenuItems(
						"Added Component",
						instanceGo,
						(menuItemContent, sourceObject) => {
							GameObject rootObject = GetRootGameObject(sourceObject);
							if(!PrefabUtility.IsPartOfPrefabThatCanBeAppliedTo(rootObject) || EditorUtility.IsPersistent(instanceGo))
							{
								menu.AddDisabledItem(menuItemContent);
							}
							else
							{
								menu.AddItem(menuItemContent, false, utils.ApplyPrefabAddedComponent,
									utils.ObjectInstanceAndSourcePathInfoCtor(targetComponent, AssetDatabase.GetAssetPath(sourceObject)));
							}
						},
						(menuItemContent) => {
							menu.AddItem(menuItemContent, false, utils.RevertPrefabAddedComponent, targetComponent);
						},
						false
					);
				}
				else
				{
					bool hasPrefabOverride = false;
					using(var so = new SerializedObject(targetComponent))
					{
						SerializedProperty property = so.GetIterator();
						while(property.Next(property.hasChildren))
						{
							if(property.isInstantiatedPrefab && property.prefabOverride && !property.isDefaultOverride)
							{
								hasPrefabOverride = true;
								break;
							}
						}
					}

					// Handle modified component.
					if(hasPrefabOverride)
					{
						bool defaultOverrides = utils.IsObjectOverrideAllDefaultOverridesComparedToAnySource(targetComponent);

						utils.HandleApplyRevertMenuItems(
							"Modified Component", targetComponent,
							(menuItemContent, sourceObject) => {
								GameObject rootObject = GetRootGameObject(sourceObject);
								if(!PrefabUtility.IsPartOfPrefabThatCanBeAppliedTo(rootObject) || EditorUtility.IsPersistent(targetComponent))
								{
									menu.AddDisabledItem(menuItemContent);
								}
								else
								{
									menu.AddItem(menuItemContent, false, utils.ApplyPrefabObjectOverride,
										utils.ObjectInstanceAndSourcePathInfoCtor(targetComponent, AssetDatabase.GetAssetPath(sourceObject)));
								}
							},
							(menuItemContent) => {
								menu.AddItem(menuItemContent, false, utils.RevertPrefabObjectOverride, targetComponent);
							},
							defaultOverrides
						);
					}
				}
			}

			private static GameObject GetRootGameObject(UnityEngine.Object componentOrGameObject)
			{
				GameObject gameObject = componentOrGameObject is GameObject go ? go : (componentOrGameObject is Component c ? c.gameObject : null);
				if(gameObject == null) return null;
				return gameObject.transform.root.gameObject;
			}

			private class ContextMenuItem
			{
				private string name;
				private MonoBehaviour target;
				public MethodInfo condition = null;
				public MethodInfo action = null;
				public int order = 0;

				public ContextMenuItem(string name, MonoBehaviour target)
				{
					this.name = name;
					this.target = target;
				}

				public bool CanBeAdded()
				{
					return action != null;
				}

				public void AddToMenu(GenericMenu menu)
				{
					if(condition == null || (bool)condition.Invoke(target, new object[0]))
					{
						menu.AddItem(new GUIContent(name), false, OnSelected);
					}
					else
					{
						menu.AddDisabledItem(new GUIContent(name));
					}
				}

				private void OnSelected()
				{
					action.Invoke(target, new object[0]);
				}
			}
		}

		private class UnityEditorUtils
		{
			private const BindingFlags INTERNAL_BIND = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

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
			public readonly Func<UnityEngine.Object[], Event, Component, bool> ShouldDrawOverrideBackground;
			public readonly Action<Rect, bool> DrawOverrideBackground;

			public readonly Action<MenuCommand> FindComponentReferencesOnScene;
			public readonly Action ForceRebuildInspectors;

			public Action<string, UnityEngine.Object, Action<GUIContent, UnityEngine.Object>, Action<GUIContent>, bool> HandleApplyRevertMenuItems = null;
			public Func<UnityEngine.Object, bool> IsObjectOverrideAllDefaultOverridesComparedToAnySource;
			public Func<UnityEngine.Object, string, object> ObjectInstanceAndSourcePathInfoCtor;
			public GenericMenu.MenuFunction2 ApplyPrefabAddedComponent;
			public GenericMenu.MenuFunction2 RevertPrefabAddedComponent;
			public GenericMenu.MenuFunction2 ApplyPrefabObjectOverride;
			public GenericMenu.MenuFunction2 RevertPrefabObjectOverride;

			public readonly Type DragAndDropDelay;
			public readonly FieldInfo dragAndDropDelayField;
			public readonly MethodInfo dragAndDropDelayMethod;

			public readonly object InspectorEditorDraggingMode;

			public readonly Texture2D prefabOverlayAddedIcon;

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
				prefabOverlayAddedIcon = GetFieldValue<Texture2D>(typeof(EditorGUI).GetNestedType("Styles", INTERNAL_BIND), "prefabOverlayAddedIcon");

				_tempContentTexture = CreateFunc<Texture, GUIContent>(typeof(EditorGUIUtility), "TempContent");
				_tempContentText = CreateFunc<string, GUIContent>(typeof(EditorGUIUtility), "TempContent");

				InAnimationRecording = CreateFunc<bool>(typeof(AnimationMode), "InAnimationRecording");
				IsPropertyCandidate = CreateFunc<UnityEngine.Object, string, bool>(typeof(AnimationMode), "IsPropertyCandidate");
				InspectorWindowDrag = CreateFunc<UnityEngine.Object[], bool, DragAndDropVisualMode>(typeof(InternalEditorUtility), "InspectorWindowDrag");
				DoPropertyContextMenu = CreateAction<SerializedProperty, SerializedProperty, GenericMenu>(typeof(EditorGUI), "DoPropertyContextMenu");
				DrawEditorHeaderItems = CreateFunc<Rect, UnityEngine.Object[], float, Rect>(typeof(EditorGUIUtility), "DrawEditorHeaderItems");

				ShouldDrawOverrideBackground = CreateFunc<UnityEngine.Object[], Event, Component, bool>(typeof(EditorGUI), "ShouldDrawOverrideBackground");
				DrawOverrideBackground = CreateAction<Rect, bool>(typeof(EditorGUI), "DrawOverrideBackground");

				DragAndDropDelay = typeof(EditorGUI).Assembly.GetType("UnityEditor.DragAndDropDelay");
				dragAndDropDelayField = DragAndDropDelay.GetField("mouseDownPosition");
				dragAndDropDelayMethod = DragAndDropDelay.GetMethod("CanStartDrag");

				var EditorDraggingMode = typeof(EditorGUI).Assembly.GetType("UnityEditor.EditorDragging").GetNestedType("DraggingMode", BindingFlags.NonPublic);
				var EditorDraggingModeNullable = typeof(Nullable<>).MakeGenericType(EditorDraggingMode);
				InspectorEditorDraggingMode = Activator.CreateInstance(EditorDraggingModeNullable, Enum.ToObject(EditorDraggingMode, 0));

				FindComponentReferencesOnScene = CreateAction<MenuCommand>(typeof(SearchableEditorWindow), "OnSearchForReferencesToComponent");
				ForceRebuildInspectors = CreateAction(typeof(EditorUtility), "ForceRebuildInspectors");
			}

			public GUIContent TempContent(Texture texture)
			{
				return _tempContentTexture(texture);
			}

			public GUIContent TempContent(string text)
			{
				return _tempContentText(text);
			}

			public void InitPrefabUtils()
			{
				if(HandleApplyRevertMenuItems != null) return;
				HandleApplyRevertMenuItems = CreateAction<string, UnityEngine.Object, Action<GUIContent, UnityEngine.Object>, Action<GUIContent>, bool>(typeof(PrefabUtility), "HandleApplyRevertMenuItems");
				IsObjectOverrideAllDefaultOverridesComparedToAnySource = CreateFunc<UnityEngine.Object, bool>(typeof(PrefabUtility), "IsObjectOverrideAllDefaultOverridesComparedToAnySource");

				var targetChoiceHandler = typeof(PrefabUtility).Assembly.GetType("UnityEditor.TargetChoiceHandler");
				ApplyPrefabAddedComponent = CreateMenuCallback(targetChoiceHandler, "ApplyPrefabAddedComponent");
				RevertPrefabAddedComponent = CreateMenuCallback(targetChoiceHandler, "RevertPrefabAddedComponent");
				ApplyPrefabObjectOverride = CreateMenuCallback(targetChoiceHandler, "ApplyPrefabObjectOverride");
				RevertPrefabObjectOverride = CreateMenuCallback(targetChoiceHandler, "RevertPrefabObjectOverride");

				var childType = targetChoiceHandler.GetNestedType("ObjectInstanceAndSourcePathInfo", INTERNAL_BIND);
				var objParameter = Expression.Parameter(typeof(UnityEngine.Object));
				var pathParameter = Expression.Parameter(typeof(string));
				var tmpVariable = Expression.Variable(childType);
				ObjectInstanceAndSourcePathInfoCtor = Expression.Lambda<Func<UnityEngine.Object, string, object>>(
					Expression.Block(new ParameterExpression[] { tmpVariable },
						Expression.Assign(Expression.Field(tmpVariable, "instanceObject"), objParameter),
						Expression.Assign(Expression.Field(tmpVariable, "assetPath"), pathParameter),
						Expression.Convert(tmpVariable, typeof(object))
					),
					objParameter, pathParameter
				).Compile();
			}

			private static TOut GetFieldValue<TOut>(Type type, string name)
			{
				return (TOut)type.GetField(name, INTERNAL_BIND).GetValue(null);
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

			private static Action<TIn0, TIn1, TIn2, TIn3, TIn4> CreateAction<TIn0, TIn1, TIn2, TIn3, TIn4>(Type type, string name)
			{
				return (Action<TIn0, TIn1, TIn2, TIn3, TIn4>)(object)Delegate.CreateDelegate(typeof(Action<TIn0, TIn1, TIn2, TIn3, TIn4>), type.GetMethod(name, INTERNAL_BIND, null, new Type[] { typeof(TIn0), typeof(TIn1), typeof(TIn2), typeof(TIn3), typeof(TIn4) }, null));
			}

			private static Action<TIn0, TIn1, TIn2> CreateAction<TIn0, TIn1, TIn2>(Type type, string name)
			{
				return (Action<TIn0, TIn1, TIn2>)(object)Delegate.CreateDelegate(typeof(Action<TIn0, TIn1, TIn2>), type.GetMethod(name, INTERNAL_BIND, null, new Type[] { typeof(TIn0), typeof(TIn1), typeof(TIn2) }, null));
			}

			private static Action<TIn0, TIn1> CreateAction<TIn0, TIn1>(Type type, string name)
			{
				return (Action<TIn0, TIn1>)(object)Delegate.CreateDelegate(typeof(Action<TIn0, TIn1>), type.GetMethod(name, INTERNAL_BIND, null, new Type[] { typeof(TIn0), typeof(TIn1) }, null));
			}

			private static Action<TIn0> CreateAction<TIn0>(Type type, string name)
			{
				return (Action<TIn0>)(object)Delegate.CreateDelegate(typeof(Action<TIn0>), type.GetMethod(name, INTERNAL_BIND, null, new Type[] { typeof(TIn0) }, null));
			}

			private static Action CreateAction(Type type, string name)
			{
				return (Action)(object)Delegate.CreateDelegate(typeof(Action), type.GetMethod(name, INTERNAL_BIND, null, Type.EmptyTypes, null));
			}

			private static GenericMenu.MenuFunction2 CreateMenuCallback(Type type, string name)
			{
				return (GenericMenu.MenuFunction2)(object)Delegate.CreateDelegate(typeof(GenericMenu.MenuFunction2), type.GetMethod(name, INTERNAL_BIND, null, new Type[] { typeof(object) }, null));
			}
		}
	}
}