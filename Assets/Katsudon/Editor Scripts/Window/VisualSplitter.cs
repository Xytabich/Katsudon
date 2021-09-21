using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Katsudon.Editor.UIElements
{
	internal class VisualSplitter : ImmediateModeElement
	{
		const int kDefaultSplitSize = 10;
		public int splitSize = kDefaultSplitSize;

		private class SplitManipulator : MouseManipulator
		{
			private int m_ActiveVisualElementIndex = -1;

			private List<VisualElement> m_AffectedElements;

			bool m_Active;

			public SplitManipulator()
			{
				activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
			}

			protected override void RegisterCallbacksOnTarget()
			{
				target.RegisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.TrickleDown);
				target.RegisterCallback<MouseMoveEvent>(OnMouseMove, TrickleDown.TrickleDown);
				target.RegisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);
			}

			protected override void UnregisterCallbacksFromTarget()
			{
				target.UnregisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.TrickleDown);
				target.UnregisterCallback<MouseMoveEvent>(OnMouseMove, TrickleDown.TrickleDown);
				target.UnregisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);
			}

			protected void OnMouseDown(MouseDownEvent e)
			{
				if(CanStartManipulation(e))
				{
					VisualSplitter visualSplitter = target as VisualSplitter;
					FlexDirection flexDirection = visualSplitter.resolvedStyle.flexDirection;

					if(m_AffectedElements != null)
					{
						CollectionCache.Release(m_AffectedElements);
					}
					m_AffectedElements = visualSplitter.GetAffectedVisualElements();

					for(int i = 0; i < m_AffectedElements.Count; ++i)
					{
						VisualElement visualElement = m_AffectedElements[i];

						Rect splitterRect = visualSplitter.GetSplitterRect(visualElement);

						if(splitterRect.Contains(e.localMousePosition))
						{
							bool isReverse = flexDirection == FlexDirection.RowReverse || flexDirection == FlexDirection.ColumnReverse;

							if(isReverse)
							{
								m_ActiveVisualElementIndex = i + 1;
							}
							else
							{
								m_ActiveVisualElementIndex = i;
							}

							m_Active = true;
							target.CaptureMouse();
							e.StopPropagation();
						}
					}
				}
			}

			protected void OnMouseMove(MouseMoveEvent e)
			{
				if(m_Active)
				{
					VisualSplitter visualSplitter = target as VisualSplitter;
					VisualElement visualElement = m_AffectedElements[m_ActiveVisualElementIndex];

					FlexDirection flexDirection = visualSplitter.resolvedStyle.flexDirection;
					bool isVertical = flexDirection == FlexDirection.Column || flexDirection == FlexDirection.ColumnReverse;

					float size;
					if(isVertical)
					{
						float minHeight = visualElement.resolvedStyle.minHeight == StyleKeyword.Auto ? 0f : visualElement.resolvedStyle.minHeight.value;
						float maxHeight = visualElement.resolvedStyle.maxHeight.value <= 0f ? float.PositiveInfinity : visualElement.resolvedStyle.maxHeight.value;
						size = Mathf.Clamp(e.localMousePosition.y - visualElement.layout.yMin, minHeight, maxHeight);
					}
					else
					{
						float minWidth = visualElement.resolvedStyle.minWidth == StyleKeyword.Auto ? 0 : visualElement.resolvedStyle.minWidth.value;
						float maxWidth = visualElement.resolvedStyle.maxWidth.value <= 0 ? float.PositiveInfinity : visualElement.resolvedStyle.maxWidth.value;

						size = Mathf.Clamp(e.localMousePosition.x - visualElement.layout.xMin, minWidth, maxWidth);
					}

					visualElement.style.flexBasis = size;

					e.StopPropagation();
				}
			}

			protected void OnMouseUp(MouseUpEvent e)
			{
				if(m_Active && CanStopManipulation(e))
				{
					m_Active = false;
					target.ReleaseMouse();
					e.StopPropagation();

					m_ActiveVisualElementIndex = -1;
				}
			}
		}

		public static readonly string ussClassName = "unity-visual-splitter";

		public VisualSplitter()
		{
			AddToClassList(ussClassName);
			this.AddManipulator(new SplitManipulator());
		}

		public List<VisualElement> GetAffectedVisualElements()
		{
			List<VisualElement> elements = CollectionCache.GetList<VisualElement>();
			var count = hierarchy.childCount;
			for(int i = 0; i < count; ++i)
			{
				VisualElement element = hierarchy[i];
				if(element.resolvedStyle.position == Position.Relative)
					elements.Add(element);
			}

			return elements;
		}

		protected override void ImmediateRepaint()
		{
			UpdateCursorRects();
		}

		void UpdateCursorRects()
		{
			var count = hierarchy.childCount;
			bool isVertical = resolvedStyle.flexDirection == FlexDirection.Column || resolvedStyle.flexDirection == FlexDirection.ColumnReverse;
			for(int i = 0; i < count; ++i)
			{
				VisualElement visualElement = hierarchy[i];
				if(isVertical)
				{
					if(visualElement.style.minHeight != StyleKeyword.Auto &&
						visualElement.style.minHeight == visualElement.style.maxHeight) continue;
				}
				else
				{
					if(visualElement.style.minWidth != StyleKeyword.Auto &&
						visualElement.style.minWidth == visualElement.style.maxWidth) continue;
				}

				EditorGUIUtility.AddCursorRect(GetSplitterRect(visualElement), isVertical ? MouseCursor.ResizeVertical : MouseCursor.SplitResizeLeftRight);
			}
		}

		public Rect GetSplitterRect(VisualElement visualElement)
		{
			Rect layoutRect = visualElement.layout;
			Rect outRect = visualElement.layout;
			if(resolvedStyle.flexDirection == FlexDirection.Row)
			{
				outRect.xMin = layoutRect.xMax - splitSize * 0.5f;
				outRect.xMax = layoutRect.xMax + splitSize * 0.5f;
			}
			else if(resolvedStyle.flexDirection == FlexDirection.RowReverse)
			{
				outRect.xMin = layoutRect.xMin - splitSize * 0.5f;
				outRect.xMax = layoutRect.xMin + splitSize * 0.5f;
			}
			else if(resolvedStyle.flexDirection == FlexDirection.Column)
			{
				outRect.yMin = layoutRect.yMax - splitSize * 0.5f;
				outRect.yMax = layoutRect.yMax + splitSize * 0.5f;
			}
			else if(resolvedStyle.flexDirection == FlexDirection.ColumnReverse)
			{
				outRect.yMin = layoutRect.yMin - splitSize * 0.5f;
				outRect.yMax = layoutRect.yMin + splitSize * 0.5f;
			}

			return outRect;
		}
	}
}