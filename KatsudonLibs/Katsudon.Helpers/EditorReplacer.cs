using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using UnityEditor;

namespace Katsudon.Helpers
{
	public static class EditorReplacer
	{
		private delegate void SetMainEditorFunc(Type editType, Type editorType, bool multiEdit);
		private delegate Type FindEditorTypeFunc(Type editType, bool multiEdit);
		private delegate Type FindFallbackEditorTypeFunc(Type editType, Type[] ignoreEditors, bool multiEdit);

		private static SetMainEditorFunc setMainEditor = null;
		private static FindFallbackEditorTypeFunc findFallbackEditorType = null;
		private static FindEditorTypeFunc findEditorType = null;

		private static Action _initEditorsIfNeeded = null;
		public static Action InitEditorsIfNeeded
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

		public static void SetMainEditor(Type editType, Type editorType, bool multiEdit)
		{
			InitEditorsIfNeeded();
			if(setMainEditor == null)
			{
				var typeContainer = typeof(CustomEditor).Assembly.GetType("UnityEditor.CustomEditorAttributes+MonoEditorType");
				var attributesCollection = typeof(CustomEditor).Assembly.GetType("UnityEditor.CustomEditorAttributes");
				var kSCustomEditors = attributesCollection.GetField("kSCustomEditors", BindingFlags.NonPublic | BindingFlags.Static);
				var kSCustomMultiEditors = attributesCollection.GetField("kSCustomEditors", BindingFlags.NonPublic | BindingFlags.Static);

				var editTypeArg = Expression.Parameter(typeof(Type));
				var editorTypeArg = Expression.Parameter(typeof(Type));
				var multiArg = Expression.Parameter(typeof(bool));
				var typesDict = Expression.Variable(kSCustomEditors.FieldType);
				var editorsList = Expression.Variable(typeof(List<>).MakeGenericType(typeContainer));
				var index = Expression.Variable(typeof(int));
				var breakLoop = Expression.Label();
				setMainEditor = Expression.Lambda<SetMainEditorFunc>(
					Expression.Block(new ParameterExpression[] { typesDict, editorsList, index },
						Expression.IfThenElse(multiArg,
							Expression.Assign(typesDict, Expression.Field(null, kSCustomMultiEditors)),
							Expression.Assign(typesDict, Expression.Field(null, kSCustomEditors))
						),
						Expression.Assign(editorsList, Expression.Property(typesDict, "Item", editTypeArg)),
						Expression.Assign(index, Expression.Constant((int)0)),
						Expression.Loop(Expression.IfThenElse(
							Expression.LessThan(index, Expression.Property(editorsList, "Count")),
							Expression.Block(
								Expression.Assign(Expression.Field(Expression.Property(editorsList, "Item", index), "m_IsFallback"),
									Expression.NotEqual(Expression.Field(Expression.Property(editorsList, "Item", index), "m_InspectorType"), editorTypeArg)),
								Expression.PostIncrementAssign(index)
							),
							Expression.Break(breakLoop)
						), breakLoop)
					),
					editTypeArg, editorTypeArg, multiArg
				).Compile();
			}
			setMainEditor(editType, editorType, multiEdit);
		}

		public static Type GetFallbackEditor(Type editType, bool multiEdit)
		{
			if(editType == null) return null;
			if(findFallbackEditorType == null) InitSearch();
			InitEditorsIfNeeded();
			return findFallbackEditorType(editType, Type.EmptyTypes, multiEdit);
		}

		public static Type GetFallbackEditor(Type editType, Type[] ignoreEditors, bool multiEdit)
		{
			if(editType == null) return null;
			if(findFallbackEditorType == null) InitSearch();
			InitEditorsIfNeeded();
			return findFallbackEditorType(editType, ignoreEditors, multiEdit);
		}

		public static Type GetEditor(Type editType, bool multiEdit)
		{
			if(editType == null) return null;
			if(findEditorType == null)
			{
				var editorsType = typeof(CustomEditor).Assembly.GetType("UnityEditor.CustomEditorAttributes");
				findEditorType = (FindEditorTypeFunc)Delegate.CreateDelegate(typeof(FindEditorTypeFunc), null,
					editorsType.GetMethod("FindCustomEditorTypeByType", BindingFlags.Static | BindingFlags.NonPublic));
			}
			return findEditorType(editType, multiEdit);
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
			var ignoreTypes = Expression.Parameter(typeof(Type[]));
			var multiEdit = Expression.Parameter(typeof(bool));
			var dictionary = Expression.Variable(dictType);
			var list = Expression.Variable(listType);
			var enumerator = Expression.Variable(listEnumeratorType);
			var breakLabel = Expression.Label();
			var returnLabel = Expression.Label(typeof(Type));
			var enumeratorCurrent = Expression.Property(enumerator, "Current");

			Expression<Func<Type, Type[], bool>> notIn = (type, types) => NotInArray(type, types);
			findFallbackEditorType = Expression.Lambda<FindFallbackEditorTypeFunc>(Expression.Block(typeof(Type),
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
									Expression.IfThen(
										Expression.Invoke(notIn, Expression.Field(enumeratorCurrent, "m_InspectorType"), ignoreTypes),
										Expression.Return(returnLabel, Expression.Field(enumeratorCurrent, "m_InspectorType"))
									)
								)
							),
							breakLabel
						)
					)
				),
				Expression.Label(returnLabel, Expression.Constant(null, typeof(Type)))
			), editType, ignoreTypes, multiEdit).Compile();
		}

		private static bool NotInArray(Type type, Type[] types)
		{
			return Array.IndexOf(types, type) < 0;
		}
	}
}