using System;
using System.Reflection;
using UnityEngine;

namespace Katsudon.Editor
{
	public abstract class EditorProxyDrawer : UnityEditor.Editor
	{
		private IEditorProxy proxy = null;

		protected virtual void OnEnable()
		{
			OnInit();
		}

		protected virtual void OnDisable()
		{
			if(proxy != null)
			{
				proxy.Dispose();
				proxy = null;
			}
		}

		/// <summary>
		/// Call <see cref="CreateEditor"> from here if needed
		/// </summary>
		protected abstract void OnInit();

		protected void CreateEditor(Type editorType)
		{
			UnityEditor.Editor editor = null;
			if(proxy != null)
			{
				if(proxy is EditorProxy ep && ep.editor.GetType() == editorType)
				{
					editor = ep.editor;
				}
				else
				{
					proxy.Dispose();
					proxy = null;
				}
			}
			CreateCachedEditor(targets, editorType, ref editor);
			if(editor != null)
			{
				proxy = CreateProxy(editor);
			}
			else proxy = null;
		}

		protected IEditorProxy CreateProxy(UnityEditor.Editor editor)
		{
			return proxy = new EditorProxy(editor);
		}

		protected void SetProxy(IEditorProxy proxy)
		{
			this.proxy = proxy;
		}

		public override void DrawPreview(Rect previewArea)
		{
			if(proxy != null) proxy.DrawPreview(previewArea);
			else base.DrawPreview(previewArea);
		}

		public override bool HasPreviewGUI()
		{
			if(proxy != null) return proxy.HasPreviewGUI();
			else return base.HasPreviewGUI();
		}

		public override string GetInfoString()
		{
			if(proxy != null) return proxy.GetInfoString();
			else return base.GetInfoString();
		}

		public override GUIContent GetPreviewTitle()
		{
			if(proxy != null) return proxy.GetPreviewTitle();
			else return base.GetPreviewTitle();
		}

		public override void OnInteractivePreviewGUI(Rect r, GUIStyle background)
		{
			if(proxy != null) proxy.OnInteractivePreviewGUI(r, background);
			else base.OnInteractivePreviewGUI(r, background);
		}

		public override void OnPreviewGUI(Rect r, GUIStyle background)
		{
			if(proxy != null) proxy.OnPreviewGUI(r, background);
			else base.OnPreviewGUI(r, background);
		}

		public override void OnPreviewSettings()
		{
			if(proxy != null) proxy.OnPreviewSettings();
			else base.OnPreviewSettings();
		}

		public override void ReloadPreviewInstances()
		{
			if(proxy != null) proxy.ReloadPreviewInstances();
			else base.ReloadPreviewInstances();
		}

		public override Texture2D RenderStaticPreview(string assetPath, UnityEngine.Object[] subAssets, int width, int height)
		{
			if(proxy != null) return proxy.RenderStaticPreview(assetPath, subAssets, width, height);
			else return base.RenderStaticPreview(assetPath, subAssets, width, height);
		}

		public override bool UseDefaultMargins()
		{
			if(proxy != null) return proxy.UseDefaultMargins();
			else return base.UseDefaultMargins();
		}

		public override void OnInspectorGUI()
		{
			if(proxy != null) proxy.OnInspectorGUI();
			else base.OnInspectorGUI();
		}

		public override bool RequiresConstantRepaint()
		{
			if(proxy != null) return proxy.RequiresConstantRepaint();
			else return base.RequiresConstantRepaint();
		}

		protected override void OnHeaderGUI()
		{
			if(proxy != null) proxy.OnHeaderGUI();
			else base.OnHeaderGUI();
		}

		protected virtual void OnSceneGUI()
		{
			if(proxy != null) proxy.OnSceneGUI();
		}

		protected virtual bool HasFrameBounds()
		{
			if(proxy != null) return proxy.HasFrameBounds();
			return false;
		}

		protected virtual Bounds OnGetFrameBounds()
		{
			if(proxy != null) return proxy.OnGetFrameBounds();
			return default;
		}

		protected override bool ShouldHideOpenButton()
		{
			if(proxy != null) return proxy.ShouldHideOpenButton();
			else return base.ShouldHideOpenButton();
		}

		protected class EditorProxy : IEditorProxy, IDisposable
		{
			public UnityEditor.Editor editor = null;

			// There is no need for awake and destroy messages because they are called automatically when an object is created or destroyed
			private Action onSceneGUIMessage = null;
			private Func<bool> hasFrameBoundsMessage = null;
			private Func<Bounds> onGetFrameBoundsMessage = null;
			private Action onHeaderGUICallback = null;
			private Func<bool> shouldHideOpenButtonCallback = null;

			public EditorProxy(UnityEditor.Editor editor)
			{
				this.editor = editor;
				var type = editor.GetType();

				const BindingFlags MESSAGE_FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

				var onSceneGUI = type.GetMethod(nameof(OnSceneGUI), MESSAGE_FLAGS, null, Type.EmptyTypes, null);
				onSceneGUIMessage = onSceneGUI == null ? null : CreateDelegate<Action>(editor, onSceneGUI);

				var hasFrameBounds = type.GetMethod(nameof(HasFrameBounds), MESSAGE_FLAGS, null, Type.EmptyTypes, null);
				hasFrameBoundsMessage = hasFrameBounds == null ? null : CreateDelegate<Func<bool>>(editor, hasFrameBounds);

				var onGetFrameBounds = type.GetMethod(nameof(OnGetFrameBounds), MESSAGE_FLAGS, null, Type.EmptyTypes, null);
				onGetFrameBoundsMessage = onGetFrameBounds == null ? null : CreateDelegate<Func<Bounds>>(editor, onGetFrameBounds);

				var onHeaderGUI = type.GetMethod(nameof(OnHeaderGUI), BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
				onHeaderGUICallback = CreateDelegate<Action>(editor, onHeaderGUI);

				var shouldHideOpenButton = type.GetMethod(nameof(ShouldHideOpenButton), BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
				shouldHideOpenButtonCallback = CreateDelegate<Func<bool>>(editor, shouldHideOpenButton);
			}

			public void DrawPreview(Rect previewArea)
			{
				editor.DrawPreview(previewArea);
			}

			public bool HasPreviewGUI()
			{
				return editor.HasPreviewGUI();
			}

			public string GetInfoString()
			{
				return editor.GetInfoString();
			}

			public GUIContent GetPreviewTitle()
			{
				return editor.GetPreviewTitle();
			}

			public void OnInteractivePreviewGUI(Rect r, GUIStyle background)
			{
				editor.OnInteractivePreviewGUI(r, background);
			}

			public void OnPreviewGUI(Rect r, GUIStyle background)
			{
				editor.OnPreviewGUI(r, background);
			}

			public void OnPreviewSettings()
			{
				editor.OnPreviewSettings();
			}

			public void ReloadPreviewInstances()
			{
				editor.ReloadPreviewInstances();
			}

			public Texture2D RenderStaticPreview(string assetPath, UnityEngine.Object[] subAssets, int width, int height)
			{
				return editor.RenderStaticPreview(assetPath, subAssets, width, height);
			}

			public bool UseDefaultMargins()
			{
				return editor.UseDefaultMargins();
			}

			public void OnInspectorGUI()
			{
				editor.OnInspectorGUI();
			}

			public bool RequiresConstantRepaint()
			{
				return editor.RequiresConstantRepaint();
			}

			public void OnHeaderGUI()
			{
				onHeaderGUICallback();
			}

			public void OnSceneGUI()
			{
				if(onSceneGUIMessage != null)
				{
					onSceneGUIMessage();
				}
			}

			public bool HasFrameBounds()
			{
				if(hasFrameBoundsMessage != null)
				{
					return hasFrameBoundsMessage();
				}
				return false;
			}

			public Bounds OnGetFrameBounds()
			{
				if(onGetFrameBoundsMessage != null)
				{
					return onGetFrameBoundsMessage();
				}
				return default;
			}

			public bool ShouldHideOpenButton()
			{
				return shouldHideOpenButtonCallback();
			}

			public void Dispose()
			{
				DestroyImmediate(editor);
				editor = null;
				onSceneGUIMessage = null;
				hasFrameBoundsMessage = null;
				onGetFrameBoundsMessage = null;
				onHeaderGUICallback = null;
				shouldHideOpenButtonCallback = null;
			}

			private static T CreateDelegate<T>(UnityEditor.Editor editor, MethodInfo method)
			{
				return (T)(object)Delegate.CreateDelegate(typeof(T), editor, method);
			}
		}

		protected interface IEditorProxy : IDisposable
		{
			bool HasPreviewGUI();

			string GetInfoString();

			GUIContent GetPreviewTitle();

			void DrawPreview(Rect previewArea);

			void OnInteractivePreviewGUI(Rect r, GUIStyle background);

			void OnPreviewGUI(Rect r, GUIStyle background);

			void OnPreviewSettings();

			void ReloadPreviewInstances();

			Texture2D RenderStaticPreview(string assetPath, UnityEngine.Object[] subAssets, int width, int height);

			bool UseDefaultMargins();

			void OnInspectorGUI();

			bool RequiresConstantRepaint();

			void OnHeaderGUI();

			void OnSceneGUI();

			bool HasFrameBounds();

			Bounds OnGetFrameBounds();

			bool ShouldHideOpenButton();
		}
	}
}