using UnityEditor;
using System;
using VRC.Udon.ProgramSources;
using VRC.Udon;
using System.Linq.Expressions;
using System.Reflection;
using System.Collections.Generic;

namespace Katsudon.Editor
{
	internal static class EditorReplacer
	{
		private delegate void SetMainEditorFunc(Type editType, Type editorType);
		private delegate Type FindFallbackEditorTypeFunc(Type editType, bool multiEdit);
		private static FindFallbackEditorTypeFunc FindEditorType = null;

		private static Action _initEditorsIfNeeded = null;
		private static Action InitEditorsIfNeeded
		{
			get
			{
				if(_initEditorsIfNeeded == null)
				{
					var editorsType = typeof(CustomEditor).Assembly.GetType("UnityEditor.CustomEditorAttributes");
					var isInitedField = Expression.Field(null, editorsType.GetField("s_Initialized", BindingFlags.NonPublic | BindingFlags.Static));
					_initEditorsIfNeeded = Expression.Lambda<Action>(
						Expression.IfThen(Expression.Not(isInitedField), Expression.Block(
							Expression.Call(
								editorsType.GetMethod("FindCustomEditorTypeByType", BindingFlags.NonPublic | BindingFlags.Static),
								Expression.Constant(null, typeof(Type)),
								Expression.Constant(false)
							),
							Expression.Assign(isInitedField, Expression.Constant(true))
						))
					).Compile();
				}
				return _initEditorsIfNeeded;
			}
		}

		[InitializeOnLoadMethod]
		private static void Init()
		{
			InitEditorsIfNeeded();
			ReplaceEditors();
		}

		internal static Type GetFallbackEditor(Type editType, bool multiEdit)
		{
			if(editType == null) return null;
			if(FindEditorType == null) InitSearch();
			InitEditorsIfNeeded();
			return FindEditorType(editType, multiEdit);
		}

		private static void ReplaceEditors()
		{
			var typeContainer = typeof(CustomEditor).Assembly.GetType("UnityEditor.CustomEditorAttributes+MonoEditorType");
			var attributesCollection = typeof(CustomEditor).Assembly.GetType("UnityEditor.CustomEditorAttributes");
			var typesDict = attributesCollection.GetField("kSCustomEditors", BindingFlags.NonPublic | BindingFlags.Static);

			var editTypeArg = Expression.Parameter(typeof(Type));
			var editorTypeArg = Expression.Parameter(typeof(Type));
			var editorsList = Expression.Variable(typeof(List<>).MakeGenericType(typeContainer));
			var index = Expression.Variable(typeof(int));
			var breakLoop = Expression.Label();
			var setMainEditor = Expression.Lambda<SetMainEditorFunc>(
				Expression.Block(new ParameterExpression[] { editorsList, index },
					Expression.Assign(editorsList, Expression.Property(Expression.Field(null, typesDict), "Item", editTypeArg)),
					Expression.Assign(index, Expression.Constant((int)0)),
					Expression.Loop(Expression.IfThenElse(Expression.LessThan(index, Expression.Property(editorsList, "Count")),
						Expression.Block(
							Expression.IfThen(Expression.NotEqual(Expression.Field(Expression.Property(editorsList, "Item", index), "m_InspectorType"), editorTypeArg),
								Expression.Assign(Expression.Field(Expression.Property(editorsList, "Item", index), "m_IsFallback"), Expression.Constant(true))
							),
							Expression.PostIncrementAssign(index)
						),
						Expression.Break(breakLoop)
					), breakLoop)
				),
				editTypeArg, editorTypeArg
			).Compile();

			setMainEditor(typeof(SerializedUdonProgramAsset), typeof(ProgramAssetInspector));
			setMainEditor(typeof(UdonBehaviour), typeof(UdonBehaviourInspector));
			setMainEditor(typeof(MonoScript), typeof(ScriptInspector));
		}

		private static void InitSearch()
		{
			/*
			Dictionary<Type, List<CustomEditorAttributes.MonoEditorType>> dictionary =
				multiEdit ? CustomEditorAttributes.kSCustomMultiEditors : CustomEditorAttributes.kSCustomEditors;
			if(dictionary.TryGetValue(editType, out List<CustomEditorAttributes.MonoEditorType> list))
			{
				foreach(var item in list)
				{
					if(item.m_IsFallback)
					{
						return item.m_InspectorType;
					}
				}
			}
			return null;
			*/
			var editorsType = typeof(CustomEditor).Assembly.GetType("UnityEditor.CustomEditorAttributes");
			var metType = typeof(CustomEditor).Assembly.GetType("UnityEditor.CustomEditorAttributes+MonoEditorType");
			var listType = typeof(List<>).MakeGenericType(metType);
			var listEnumeratorType = typeof(List<>).GetNestedType("Enumerator").MakeGenericType(metType);
			var dictType = typeof(Dictionary<,>).MakeGenericType(typeof(Type), listType);

			var editType = Expression.Parameter(typeof(Type));
			var multiEdit = Expression.Parameter(typeof(bool));
			var dictionary = Expression.Variable(dictType);
			var list = Expression.Variable(listType);
			var enumerator = Expression.Variable(listEnumeratorType);
			var breakLabel = Expression.Label();
			var returnLabel = Expression.Label(typeof(Type));
			var enumeratorCurrent = Expression.Property(enumerator, "Current");

			FindEditorType = Expression.Lambda<FindFallbackEditorTypeFunc>(Expression.Block(typeof(Type),
				new ParameterExpression[] { dictionary, list, enumerator },
				Expression.Assign(dictionary, Expression.Condition(multiEdit,
					Expression.Field(null, editorsType.GetField("kSCustomMultiEditors", BindingFlags.Static | BindingFlags.NonPublic)),
					Expression.Field(null, editorsType.GetField("kSCustomEditors", BindingFlags.Static | BindingFlags.NonPublic))
				)),
				Expression.IfThen(
					Expression.Call(dictionary, dictType.GetMethod("TryGetValue"), editType, list),
					Expression.Block(
						Expression.Assign(enumerator, Expression.Call(list, listType.GetMethod("GetEnumerator"))),
						Expression.Loop(
							Expression.Block(
								Expression.IfThen(
									Expression.Not(Expression.Call(enumerator, "MoveNext", Type.EmptyTypes)),
									Expression.Break(breakLabel)
								),
								Expression.IfThen(
									Expression.Field(enumeratorCurrent, "m_IsFallback"),
									Expression.Return(returnLabel, Expression.Field(enumeratorCurrent, "m_InspectorType"))
								)
							),
							breakLabel
						)
					)
				),
				Expression.Label(returnLabel, Expression.Constant(null, typeof(Type)))
			), editType, multiEdit).Compile();
		}
	}
}