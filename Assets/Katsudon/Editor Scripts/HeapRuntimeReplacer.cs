using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Katsudon.Editor.Udon;
using Katsudon.Info;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;

namespace Katsudon.Editor
{
	internal static class HeapRuntimeReplacer
	{
		private static FieldInfo heapField = null;

		private static Dictionary<Type, SetterInfo[]> fieldsMap = null;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void OnBeforeSceneLoad()
		{
			UdonManager.OnUdonProgramLoaded -= ReplaceHeap;
			UdonManager.OnUdonProgramLoaded += ReplaceHeap;
			UdonBehaviour.OnInit -= InitHeap;
			UdonBehaviour.OnInit += InitHeap;
		}

		private static void ReplaceHeap(IUdonProgram program)
		{
			if(program is UdonProgram programInstance)
			{
				if(heapField == null) InitHeapField();
				heapField.SetValue(programInstance, new HeapTracker(program.Heap));
			}
		}

		private static void InitHeap(UdonBehaviour behaviour, IUdonProgram program)
		{
			if(program.Heap is HeapTracker heapTracker)
			{
				var proxy = ProxyUtils.GetProxyByBehaviour(behaviour);
				if(proxy != null)
				{
					if(InitFields(proxy.GetType(), program.SymbolTable, program.Heap.GetHeapCapacity(), out var map))
					{
						if(heapField == null) InitHeapField();
						heapTracker.Init(map, proxy);
					}
				}
			}
		}

		private static bool InitFields(Type type, IUdonSymbolTable symbols, uint symbolsCount, out SetterInfo[] map)
		{
			if(fieldsMap == null) fieldsMap = new Dictionary<Type, SetterInfo[]>();
			else if(fieldsMap.TryGetValue(type, out map)) return true;

			var typeInfo = AssembliesInfo.instance.GetBehaviourInfo(type);
			if(typeInfo != null)
			{
				map = new SetterInfo[symbolsCount];
				var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				var instanceParam = Expression.Parameter(typeof(object));
				var valueParam = Expression.Parameter(typeof(object));
				for(int i = 0; i < fields.Length; i++)
				{
					var info = typeInfo.GetField(fields[i]);
					if(info != null)
					{
						if(symbols.TryGetAddressFromSymbol(info.name, out uint address))
						{
							map[address] = new SetterInfo(fields[i].FieldType, Expression.Lambda<Action<object, object>>(
								Expression.Assign(
									Expression.Field(Expression.Convert(instanceParam, type), fields[i]), Expression.Convert(valueParam, fields[i].FieldType)
								),
								instanceParam, valueParam
							).Compile());
						}
					}
				}
				fieldsMap[type] = map;
				return true;
			}
			map = null;
			return false;
		}

		private static void InitHeapField()
		{
			var programParam = Expression.Parameter(typeof(UdonProgram));
			var heapParam = Expression.Parameter(typeof(IUdonHeap));
			heapField = typeof(UdonProgram).GetField("<Heap>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
		}

		private class HeapTracker : IUdonHeap
		{
			private IUdonHeap heap;
			private MonoBehaviour proxy;
			private SetterInfo[] setters;

			public HeapTracker(IUdonHeap heap)
			{
				this.heap = heap;
				setters = null;
				proxy = null;
			}

			public void Init(SetterInfo[] setters, MonoBehaviour proxy)
			{
				this.proxy = proxy;
				this.setters = setters;
			}

			public void CopyHeapVariable(uint sourceAddress, uint destAddress)
			{
				heap.CopyHeapVariable(sourceAddress, destAddress);
				if(proxy) SetValue(destAddress, heap.GetHeapVariable(sourceAddress));
			}

			public void DumpHeapObjects(List<(uint address, IStrongBox strongBoxedObject, Type objectType)> destination)
			{
				heap.DumpHeapObjects(destination);
			}

			public uint GetHeapCapacity()
			{
				return heap.GetHeapCapacity();
			}

			public object GetHeapVariable(uint address)
			{
				return heap.GetHeapVariable(address);
			}

			public T GetHeapVariable<T>(uint address)
			{
				return heap.GetHeapVariable<T>(address);
			}

			public Type GetHeapVariableType(uint address)
			{
				return heap.GetHeapVariableType(address);
			}

			public void InitializeHeapVariable(uint address, Type type)
			{
				heap.InitializeHeapVariable(address, type);
			}

			public void InitializeHeapVariable<T>(uint address)
			{
				heap.InitializeHeapVariable<T>(address);
			}

			public bool IsHeapVariableInitialized(uint address)
			{
				return heap.IsHeapVariableInitialized(address);
			}

			public void SetHeapVariable(uint address, object value, Type type)
			{
				heap.SetHeapVariable(address, value, type);
				if(proxy) SetValue(address, value);
			}

			public void SetHeapVariable<T>(uint address, T value)
			{
				heap.SetHeapVariable(address, value);
				if(proxy) SetValue(address, value);
			}

			public bool TryGetHeapVariable(uint address, out object value)
			{
				return heap.TryGetHeapVariable(address, out value);
			}

			public bool TryGetHeapVariable<T>(uint address, out T value)
			{
				return heap.TryGetHeapVariable(address, out value);
			}

			private void SetValue(uint address, object value)
			{
				if(setters[address] != null) setters[address].Set(proxy, value);
			}
		}

		private class SetterInfo
		{
			public readonly Type type;
			public readonly Action<object, object> setter;

			public SetterInfo(Type type, Action<object, object> setter)
			{
				this.type = type;
				this.setter = setter;
			}

			public void Set(MonoBehaviour proxy, object value)
			{
				if(ProxyUtils.valueResolver.TryConvertFromUdon(value, type, out var converted, out _))
				{
					setter.Invoke(proxy, converted);
				}
			}
		}
	}
}