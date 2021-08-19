using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.Udon;

namespace Katsudon.Utility
{
	[ExecuteInEditMode]
	internal class ReferencesContainer : MonoBehaviour
	{
		[SerializeField, HideInInspector]
		private List<UdonBehaviour> behaviours = new List<UdonBehaviour>();
		[SerializeField, HideInInspector]
		private List<MonoBehaviour> proxies = new List<MonoBehaviour>();

		public int pairsCount => behaviours.Count;
		public int id => _id;

		private int _id = -1;
		private Action<int> onRemoveContaienr;
		private Action<UdonBehaviour> onRemovePair;

		void OnDestroy()
		{
			onRemoveContaienr(_id);
		}

		public void Init(int id, Action<int> onRemoveContaienr, Action<UdonBehaviour> onRemovePair)
		{
			this._id = id;
			this.onRemoveContaienr = onRemoveContaienr;
			this.onRemovePair = onRemovePair;
		}

		public void UpdateReferences()
		{
			for(int i = behaviours.Count - 1; i >= 0; i--)
			{
				if(behaviours[i] == null)
				{
					onRemovePair(behaviours[i]);
				}
			}
		}

		public void AddPair(UdonBehaviour behaviour, MonoBehaviour proxy)
		{
			if(behaviours.Contains(behaviour)) return;

			behaviours.Add(behaviour);
			proxies.Add(proxy);
		}

		public PairInfo FindPair(UdonBehaviour behaviour)
		{
			int index = behaviours.IndexOf(behaviour);
			if(index < 0) return default;
			return new PairInfo(behaviours[index], proxies[index]);
		}

		public PairInfo FindPair(MonoBehaviour proxy)
		{
			int index = proxies.IndexOf(proxy);
			if(index < 0) return default;
			return new PairInfo(behaviours[index], proxies[index]);
		}

		public void RemovePair(UdonBehaviour behaviour)
		{
			int index = behaviours.IndexOf(behaviour);
			behaviours.RemoveAt(index);
			proxies.RemoveAt(index);
		}

		public IEnumerator<PairInfo> EnumeratePairs()
		{
			for(int i = behaviours.Count - 1; i >= 0; i--)
			{
				yield return new PairInfo(behaviours[i], proxies[i]);
			}
		}

		public struct PairInfo
		{
			public UdonBehaviour behaviour;
			public MonoBehaviour proxy;

			public PairInfo(UdonBehaviour behaviour, MonoBehaviour proxy)
			{
				this.behaviour = behaviour;
				this.proxy = proxy;
			}
		}
	}
}