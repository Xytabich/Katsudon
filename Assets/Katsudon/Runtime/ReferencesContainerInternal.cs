using System.Collections.Generic;
using UnityEngine;
using VRC.Udon;

namespace Katsudon.Utility
{
	internal class ReferencesContainer : MonoBehaviour, ISerializationCallbackReceiver
	{
		internal static List<ReferencesContainer> cacheContainers = null;
		internal static List<UdonBehaviour> trackBehaviours = null;
		internal static List<MonoBehaviour> trackProxies = null;

		[SerializeField]
		private List<UdonBehaviour> behaviours = new List<UdonBehaviour>();
		[SerializeField]
		private List<MonoBehaviour> proxies = new List<MonoBehaviour>();

		public bool hasPairs => behaviours.Count != 0;

		[System.NonSerialized]
		private bool isCached = false;
		[System.NonSerialized]
		private HashSet<Object> trackedObjects = new HashSet<Object>();

		public void AddPair(UdonBehaviour behaviour, MonoBehaviour proxy)
		{
			trackedObjects.Add(behaviour);
			trackedObjects.Add(proxy);

			int index = behaviours.IndexOf(behaviour);
			if(index < 0)
			{
				behaviours.Add(behaviour);
				proxies.Add(proxy);
			}
			else
			{
				proxies[index] = proxy;
			}
		}

		public MonoBehaviour RemovePair(UdonBehaviour behaviour)
		{
			int index = behaviours.IndexOf(behaviour);
			if(index < 0) return null;
			behaviours.RemoveAt(index);
			var proxy = proxies[index];
			proxies.RemoveAt(index);
			return proxy;
		}

		public MonoBehaviour GetProxyByBehaviour(UdonBehaviour behaviour)
		{
			int index = behaviours.IndexOf(behaviour);
			if(index < 0) return null;
			return proxies[index];
		}

		public UdonBehaviour GetBehaviourByProxy(MonoBehaviour proxy)
		{
			int index = proxies.IndexOf(proxy);
			if(index < 0) return null;
			return behaviours[index];
		}

		internal void RetryTrack()
		{
			foreach(var obj in behaviours)
			{
				TrackBehaviour(obj);
			}
			foreach(var obj in proxies)
			{
				TrackProxy(obj);
			}
		}

		void ISerializationCallbackReceiver.OnBeforeSerialize() { }

		void ISerializationCallbackReceiver.OnAfterDeserialize()
		{
			if(!isCached)
			{
				isCached = true;
				CacheContainer(this);
			}
			foreach(var obj in behaviours)
			{
				if(!trackedObjects.Contains(obj))
				{
					TrackBehaviour(obj);
				}
			}
			foreach(var obj in proxies)
			{
				if(!trackedObjects.Contains(obj))
				{
					TrackProxy(obj);
				}
			}
			trackedObjects.Clear();
			trackedObjects.UnionWith(behaviours);
			trackedObjects.UnionWith(proxies);
		}

		private static void CacheContainer(ReferencesContainer container)
		{
			(cacheContainers ?? (cacheContainers = new List<ReferencesContainer>())).Add(container);
		}

		private static void TrackBehaviour(UdonBehaviour behaviour)
		{
			(trackBehaviours ?? (trackBehaviours = new List<UdonBehaviour>())).Add(behaviour);
		}

		private static void TrackProxy(MonoBehaviour proxy)
		{
			(trackProxies ?? (trackProxies = new List<MonoBehaviour>())).Add(proxy);
		}
	}
}