using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Katsudon.Builder.Helpers;
using Katsudon.Info;
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

		public static void CopyFieldsToBehaviour(MonoBehaviour proxy, UdonBehaviour behaviour, ICollection<string> changeOnly = null)
		{
			var programAsset = ProgramUtils.ProgramFieldGetter(behaviour);
			if(programAsset != null)
			{
				var proxyType = AssembliesInfo.instance.GetBehaviourInfo(proxy.GetType());
				var fields = CollectionCache.GetDictionary<FieldIdentifier, AsmFieldInfo>();
				proxyType.CollectFields(fields);

				if(IsInitialized(behaviour))
				{
					foreach(var pair in fields)
					{
						if((pair.Value.flags & AsmFieldInfo.Flags.Export) != 0 && (changeOnly?.Contains(pair.Value.field.Name) ?? true))
						{
							AsmFieldUtils.TryCopyValueToBehaviour(pair.Value, proxy, behaviour.SetProgramVariable);
						}
					}
				}
				else
				{
					behaviour.publicVariables = CreateVariableTable(programAsset.RetrieveProgram());
					foreach(var pair in fields)
					{
						if((pair.Value.flags & AsmFieldInfo.Flags.Export) != 0 && (changeOnly?.Contains(pair.Value.field.Name) ?? true))
						{
							AsmFieldUtils.TryCopyValueToBehaviour(pair.Value, proxy, (name, value) => behaviour.publicVariables.TrySetVariableValue(name, value));
						}
					}
					EditorUtility.SetDirty(behaviour);
				}
			}
		}

		public static void CopyFieldsToProxy(UdonBehaviour behaviour, MonoBehaviour proxy)
		{
			var proxyType = AssembliesInfo.instance.GetBehaviourInfo(proxy.GetType());
			if(proxyType == null) return;

			var fields = CollectionCache.GetDictionary<FieldIdentifier, AsmFieldInfo>();
			proxyType.CollectFields(fields);

			if(IsInitialized(behaviour))
			{
				BehavioursTracker.IgnoreNextProxyDirtiness(proxy);
				foreach(var pair in fields)
				{
					if(AsmFieldUtils.TryCopyValueToProxy(pair.Value, proxy, behaviour.TryGetProgramVariable, out bool reserialize))
					{
						if(reserialize)
						{
							AsmFieldUtils.TryCopyValueToBehaviour(pair.Value, proxy, behaviour.SetProgramVariable);
						}
					}
				}
				EditorUtility.SetDirty(proxy);
			}
			else
			{
				bool isBehaviourChanged = false;
				var variables = behaviour.publicVariables;
				var symbols = CollectionCache.GetList(variables.VariableSymbols);
				foreach(var pair in fields)
				{
					if(AsmFieldUtils.TryCopyValueToProxy(pair.Value, proxy, variables.TryGetVariableValue, out bool reserialize))
					{
						if(reserialize)
						{
							if(AsmFieldUtils.TryCopyValueToBehaviour(pair.Value, proxy, (name, value) => variables.TrySetVariableValue(name, value)))
							{
								isBehaviourChanged = true;
							}
						}
					}
					else
					{
						variables.RemoveVariable(pair.Value.name);
						isBehaviourChanged = true;
					}
				}
				CollectionCache.Release(symbols);
				if(isBehaviourChanged) EditorUtility.SetDirty(behaviour);
			}
			CollectionCache.Release(fields);
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