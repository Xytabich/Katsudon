using System;
using System.Linq.Expressions;
using System.Reflection;
using Katsudon.Editor.Converters;
using UnityEditor;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.ProgramSources;

namespace Katsudon.Editor.Udon
{
	public static class ProxyUtils
	{
		private static UdonValueResolver _valueResolver = null;
		public static UdonValueResolver valueResolver => _valueResolver ?? (_valueResolver = new UdonValueResolver());

		private static Func<UdonBehaviour, bool> isInitializedGetter = null;

		public static MonoBehaviour GetProxyByBehaviour(UdonBehaviour behaviour)
		{
			if(behaviour.programSource != null) return null;
			var program = ProgramUtils.ProgramFieldGetter(behaviour);
			if(program == null) return null;
			return GetProxyByProgram(behaviour, program);
		}

		public static MonoBehaviour GetProxyByProgram(UdonBehaviour behaviour, SerializedUdonProgramAsset program)
		{
			var script = ProgramUtils.GetScriptByProgram(program);
			if(script == null) return null;
			return BehavioursTracker.GetOrCreateProxy(behaviour, script);
		}

		public static UdonBehaviour GetBehaviourByProxy(MonoBehaviour proxy)
		{
			return BehavioursTracker.GetBehaviourByProxy(proxy);
		}

		public static void CopyFieldsToBehaviour(MonoBehaviour proxy, UdonBehaviour behaviour)
		{
			var programAsset = ProgramUtils.ProgramFieldGetter(behaviour);
			if(programAsset != null)
			{
				var proxyType = proxy.GetType();
				var program = programAsset.RetrieveProgram();
				var symbols = program.SymbolTable;
				var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
				if(IsInitialized(behaviour))
				{
					foreach(var symbol in symbols.GetExportedSymbols())
					{
						var field = proxyType.GetField(symbol, flags);
						if(field != null && valueResolver.TryConvertToUdon(field.GetValue(proxy), out var converted))
						{
							behaviour.SetProgramVariable(symbol, converted);
						}
					}
				}
				else
				{
					behaviour.publicVariables = CreateVariableTable(program);
					foreach(var symbol in symbols.GetExportedSymbols())
					{
						var field = proxyType.GetField(symbol, flags);
						if(field != null && valueResolver.TryConvertToUdon(field.GetValue(proxy), out var converted))
						{
							behaviour.publicVariables.TrySetVariableValue(symbol, converted);
						}
					}
					EditorUtility.SetDirty(behaviour);
				}
			}
		}

		public static void CopyFieldsToProxy(UdonBehaviour behaviour, MonoBehaviour proxy)
		{
			var proxyType = proxy.GetType();
			var variables = behaviour.publicVariables;
			var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			if(IsInitialized(behaviour))
			{
				foreach(var symbol in variables.VariableSymbols)
				{
					if(behaviour.TryGetProgramVariable(symbol, out var value))
					{
						var field = proxyType.GetField(symbol, flags);
						if(field != null && valueResolver.TryConvertFromUdon(value, field.FieldType, out var converted, out bool reserialize))
						{
							field.SetValue(proxy, converted);
							if(reserialize && valueResolver.TryConvertToUdon(converted, out value))
							{
								behaviour.SetProgramVariable(symbol, value);
							}
						}
					}
				}
			}
			else
			{
				bool isBehaviourChanged = false;
				var symbols = CollectionCache.GetList(variables.VariableSymbols);
				foreach(var symbol in symbols)
				{
					if(variables.TryGetVariableValue(symbol, out var value))
					{
						var field = proxyType.GetField(symbol, flags);
						if(field != null)
						{
							if(valueResolver.TryConvertFromUdon(value, field.FieldType, out var converted, out bool reserialize))
							{
								field.SetValue(proxy, converted);
								if(reserialize && valueResolver.TryConvertToUdon(converted, out value))
								{
									variables.TrySetVariableValue(symbol, value);
									isBehaviourChanged = true;
								}
							}
							else
							{
								variables.RemoveVariable(symbol);
								isBehaviourChanged = true;
							}
						}
					}
				}
				CollectionCache.Release(symbols);
				if(isBehaviourChanged) EditorUtility.SetDirty(behaviour);
			}
		}

		public static void InitBehaviour(UdonBehaviour behaviour, MonoBehaviour proxy)
		{
			var programAsset = ProgramUtils.GetProgramByBehaviour(proxy);
			behaviour.AssignProgramAndVariables(programAsset, CreateVariableTable(programAsset.RetrieveProgram()));

			CopyFieldsToBehaviour(proxy, behaviour);
		}

		private static IUdonVariableTable CreateVariableTable(IUdonProgram program)
		{
			var symbols = program.SymbolTable;
			var heap = program.Heap;
			var variables = CollectionCache.GetList<IUdonVariable>();
			foreach(var symbol in symbols.GetExportedSymbols())
			{
				variables.Add(CreateUdonVariable(symbol, symbols.GetSymbolType(symbol)));
			}
			var table = new UdonVariableTable(variables);
			CollectionCache.Release(variables);
			return table;
		}

		private static IUdonVariable CreateUdonVariable(string symbolName, Type declaredType)
		{
			Type udonVariableType = typeof(UdonVariable<>).MakeGenericType(declaredType);
			return (IUdonVariable)Activator.CreateInstance(udonVariableType, symbolName, declaredType.IsValueType ? Activator.CreateInstance(declaredType) : null);
		}

		private static bool IsInitialized(UdonBehaviour udonBehaviour)
		{
			if(isInitializedGetter == null)
			{
				var behaviour = Expression.Parameter(typeof(UdonBehaviour));
				isInitializedGetter = Expression.Lambda<Func<UdonBehaviour, bool>>(
					Expression.Field(behaviour, typeof(UdonBehaviour).GetField("_initialized", BindingFlags.Instance | BindingFlags.NonPublic)),
				behaviour).Compile();
			}
			return isInitializedGetter.Invoke(udonBehaviour);
		}
	}
}