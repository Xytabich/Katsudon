using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Katsudon.Utility;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.Udon;

namespace Katsudon.Editor.Udon
{
	public static class BehavioursTracker
	{
		public const HideFlags SERVICE_OBJECT_FLAGS = HideFlags.DontSave | HideFlags.HideInInspector;

		private delegate IDisposable TrackObjectDelegate(UnityEngine.Object obj, Action<UnityEngine.Object> onChanged);
		private static TrackObjectDelegate _trackObject = null;

		private static Dictionary<GameObject, ReferencesContainer> containers = new Dictionary<GameObject, ReferencesContainer>();
		private static Dictionary<UdonBehaviour, GameObject> behaviours = new Dictionary<UdonBehaviour, GameObject>();
		private static Dictionary<MonoBehaviour, IDisposable> proxies = new Dictionary<MonoBehaviour, IDisposable>();
		private static HashSet<Scene> loadedScenes = new HashSet<Scene>();

		public static MonoBehaviour GetProxyByBehaviour(UdonBehaviour behaviour)
		{
			if(behaviour == null) return null;
			var container = GetContainer(behaviour.gameObject);
			if(container == null) return null;
			return container.GetProxyByBehaviour(behaviour);
		}

		public static UdonBehaviour GetBehaviourByProxy(MonoBehaviour proxy)
		{
			if(proxy == null) return null;
			var container = GetContainer(proxy.gameObject);
			if(container == null) return null;
			return container.GetBehaviourByProxy(proxy);
		}

		public static void RegisterPair(UdonBehaviour behaviour, MonoBehaviour proxy)
		{
			var group = Undo.GetCurrentGroup();
			var obj = behaviour.gameObject;
			var container = GetContainer(obj);
			if(container == null)
			{
				container = obj.AddComponent<ReferencesContainer>();
				container.hideFlags = SERVICE_OBJECT_FLAGS;
				CacheContainer(container);
			}
			container.AddPair(behaviour, proxy);
			Undo.CollapseUndoOperations(group);
			TrackBehaviour(behaviour);
			TrackProxy(proxy);
		}

		public static MonoBehaviour GetOrCreateProxy(UdonBehaviour behaviour, MonoScript script)
		{
			if(PrefabUtility.IsPartOfPrefabAsset(behaviour) || !behaviour.gameObject.scene.IsValid())
			{
				return GetTmpProxy(behaviour, script);
			}

			var proxy = GetProxyByBehaviour(behaviour);
			if(proxy == null || proxy.GetType() != script.GetClass())
			{
				var group = Undo.GetCurrentGroup();
				if(proxy != null) UnRegisterPair(behaviour);
				proxy = CreateProxy(behaviour.gameObject, behaviour, script);
				RegisterPair(behaviour, proxy);
				Undo.CollapseUndoOperations(group);
			}
			return proxy;
		}

		public static void ReleasePrefabBehaviour(UdonBehaviour behaviour)
		{
			OnBehaviourDestroyed(behaviour);
		}

		[InitializeOnLoadMethod]
		private static void Init()
		{
			EditorApplication.update += UpdateTick;
			ObjectFactory.componentWasAdded += OnComponentAdded;
			EditorSceneManager.sceneClosing += OnSceneClosed;
			EditorSceneManager.sceneOpened += OnSceneOpened;
			EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;
			EditorApplication.playModeStateChanged += OnPlayModeChanged;

			UpdateReferencesContainer();
		}

		private static void OnPlayModeChanged(PlayModeStateChange state)
		{
			if(state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode)
			{
				if(containers.Count > 0)
				{
					// Fixing missing references when changing play mode
					var keys = CollectionCache.GetList<UdonBehaviour>(behaviours.Keys);
					foreach(var behaviour in keys)
					{
						if(!TmpContainer.IsTmpBehaviour(behaviour))
						{
							behaviours.Remove(behaviour);
						}
					}
					CollectionCache.Release(keys);
					foreach(var pair in proxies)
					{
						pair.Value.Dispose();
					}
					proxies.Clear();
					foreach(var pair in containers)
					{
						pair.Value.RetryTrack();
					}
					UpdateReferencesContainer();
				}
			}
		}

		private static void UpdateTick()
		{
			UpdateReferencesContainer();
			if(BehavioursTracker.behaviours.Count > 0)
			{
				var behaviours = CollectionCache.GetList<UdonBehaviour>(BehavioursTracker.behaviours.Keys);
				foreach(var behaviour in behaviours)
				{
					if(behaviour == null)
					{
						OnBehaviourDestroyed(behaviour);
					}
				}
				CollectionCache.Release(behaviours);
			}
			if(containers.Count > 0)
			{
				var containers = CollectionCache.GetList<GameObject>(BehavioursTracker.containers.Keys);
				foreach(var container in containers)
				{
					if(container == null)
					{
						BehavioursTracker.containers.Remove(container);
					}
				}
				CollectionCache.Release(containers);
			}
		}

		private static void UpdateReferencesContainer()
		{
			var cacheContainers = ReferencesContainer.cacheContainers;
			if(cacheContainers != null && cacheContainers.Count > 0)
			{
				foreach(var container in cacheContainers)
				{
					if(container != null) CacheContainer(container);
				}
				cacheContainers.Clear();
			}
			var trackBehaviours = ReferencesContainer.trackBehaviours;
			if(trackBehaviours != null && trackBehaviours.Count > 0)
			{
				foreach(var behaviour in trackBehaviours)
				{
					if(behaviour != null) TrackBehaviour(behaviour);
				}
				trackBehaviours.Clear();
			}
			var trackProxies = ReferencesContainer.trackProxies;
			if(trackProxies != null && trackProxies.Count > 0)
			{
				foreach(var proxy in trackProxies)
				{
					if(proxy != null) TrackProxy(proxy);
				}
				trackProxies.Clear();
			}
		}

		private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
		{
			if(mode != OpenSceneMode.AdditiveWithoutLoading)
			{
				LoadScene(scene);
			}
		}

		private static void OnSceneChanged(Scene fromScene, Scene toScene)
		{
			loadedScenes.Remove(fromScene);
			LoadScene(toScene);
		}

		private static void OnSceneClosed(Scene scene, bool removingScene)
		{
			loadedScenes.Remove(scene);
		}

		private static void LoadScene(Scene scene)
		{
			if(!scene.IsValid()) return;
			if(loadedScenes.Add(scene))
			{
				foreach(var obj in scene.GetRootGameObjects())
				{
					foreach(var behaviour in obj.GetComponentsInChildren<UdonBehaviour>(true))
					{
						ProxyUtils.GetProxyByBehaviour(behaviour);
					}
				}
			}
		}

		private static void TrackBehaviour(UdonBehaviour behaviour)
		{
			if(!behaviours.ContainsKey(behaviour))
			{
				behaviours[behaviour] = behaviour.gameObject;
			}
		}

		private static void TrackProxy(MonoBehaviour proxy)
		{
			if(!proxies.ContainsKey(proxy))
			{
				proxies[proxy] = TrackObject(proxy, OnProxyChanged);
			}
		}

		private static void OnBehaviourDestroyed(UdonBehaviour behaviour)
		{
			if(!object.ReferenceEquals(behaviour, null))
			{
				if(TmpContainer.OnDestroy(behaviour))
				{
					return;
				}
				if(behaviours.TryGetValue(behaviour, out var gameObject))
				{
					if(gameObject != null) UnRegisterPair(gameObject, (UdonBehaviour)behaviour);
					else behaviours.Remove(behaviour);
				}
			}
		}

		private static void OnProxyChanged(UnityEngine.Object obj)
		{
			if(!object.ReferenceEquals(obj, null))
			{
				if(obj == null)
				{
					proxies.Remove((MonoBehaviour)obj);
				}
				else
				{
					var behaviour = GetBehaviourByProxy((MonoBehaviour)obj);
					ProxyUtils.CopyFieldsToBehaviour((MonoBehaviour)obj, behaviour);
				}
			}
		}

		private static void OnComponentAdded(Component component)
		{
			if(component is MonoBehaviour proxy)
			{
				if(Utils.IsUdonAsm(proxy.GetType()))
				{
					GetOrCreateBehaviour(proxy);
				}
			}
		}

		private static MonoBehaviour GetTmpProxy(UdonBehaviour behaviour, MonoScript script)
		{
			behaviours[behaviour] = null;
			return TmpContainer.GetOrCreateProxy(behaviour, script);
		}

		private static MonoBehaviour CreateProxy(GameObject obj, UdonBehaviour behaviour, MonoScript script)
		{
			var proxy = (MonoBehaviour)obj.AddComponent(script.GetClass());
			proxy.enabled = false;
			proxy.hideFlags = SERVICE_OBJECT_FLAGS;
			ProxyUtils.CopyFieldsToProxy(behaviour, proxy);
			return proxy;
		}

		private static UdonBehaviour GetOrCreateBehaviour(MonoBehaviour proxy)
		{
			var behaviour = GetBehaviourByProxy(proxy);
			if(behaviour == null)
			{
				var group = Undo.GetCurrentGroup();
				proxy.enabled = false;
				proxy.hideFlags = SERVICE_OBJECT_FLAGS;

				behaviour = proxy.gameObject.AddComponent<UdonBehaviour>();
				RegisterPair(behaviour, proxy);

				ProxyUtils.InitBehaviour(behaviour, proxy);
				Undo.CollapseUndoOperations(group);

				var scene = proxy.gameObject.scene;
				if(EditorApplication.isPlaying && scene.IsValid() && !EditorSceneManager.IsPreviewScene(scene))
				{
					UdonManager.Instance.RegisterUdonBehaviour(behaviour);
				}
			}
			return behaviour;
		}

		private static void UnRegisterPair(UdonBehaviour behaviour)
		{
			UnRegisterPair(behaviour.gameObject, behaviour);
		}

		private static void UnRegisterPair(GameObject obj, UdonBehaviour behaviour)
		{
			behaviours.Remove(behaviour);
			var container = GetContainer(obj);
			if(container != null)
			{
				var group = Undo.GetCurrentGroup();
				Undo.RecordObject(container, "Behaviour Remove");
				var proxy = container.RemovePair(behaviour);
				if(!object.ReferenceEquals(proxy, null))
				{
					if(proxies.TryGetValue(proxy, out var disposable))
					{
						proxies.Remove(proxy);
						disposable.Dispose();
					}
					if(proxy != null)
					{
						Undo.DestroyObjectImmediate(proxy);
					}
				}
				if(!container.hasPairs)
				{
					containers.Remove(obj);
					Undo.DestroyObjectImmediate(container);
				}
				Undo.CollapseUndoOperations(group);
			}
		}

		private static void CacheContainer(ReferencesContainer container)
		{
			containers[container.gameObject] = container;
		}

		private static ReferencesContainer GetContainer(GameObject obj)
		{
			if(!containers.TryGetValue(obj, out var container) || container == null)
			{
				container = obj.GetComponent<ReferencesContainer>();
				if(container != null) CacheContainer(container);
			}
			return container;
		}

		public static IDisposable TrackObject(UnityEngine.Object obj, Action<UnityEngine.Object> onChanged)
		{
			if(_trackObject == null)
			{
				var objParam = Expression.Parameter(typeof(UnityEngine.Object));
				var callbackParam = Expression.Parameter(typeof(Action<UnityEngine.Object>));

				var serviceType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.DataWatchService");
				_trackObject = Expression.Lambda<TrackObjectDelegate>(
					Expression.Call(Expression.Field(null, serviceType.GetField("sharedInstance")), serviceType.GetMethod("AddWatch"), objParam, callbackParam),
					objParam, callbackParam
				).Compile();
			}
			return _trackObject.Invoke(obj, onChanged);
		}

		[ExecuteInEditMode]
		private class TmpContainer : MonoBehaviour
		{
			private static TmpContainer instance = null;

			private Dictionary<UdonBehaviour, TmpProxy> tmpProxies = new Dictionary<UdonBehaviour, TmpProxy>();

			private TmpContainer()
			{
				instance = this;
			}

			public static MonoBehaviour GetOrCreateProxy(UdonBehaviour behaviour, MonoScript script)
			{
				InitInstance();
				if(instance.tmpProxies.TryGetValue(behaviour, out var info))
				{
					return info.proxy;
				}
				else
				{
					var proxy = CreateProxy(instance.gameObject, behaviour, script);
					instance.tmpProxies[behaviour] = new TmpProxy(proxy, behaviour, instance);
					return proxy;
				}
			}

			public static bool IsTmpBehaviour(UdonBehaviour behaviour)
			{
				if(instance == null) return false;
				return instance.tmpProxies.ContainsKey(behaviour);
			}

			public static bool OnDestroy(UdonBehaviour behaviour)
			{
				if(instance == null) return false;
				if(instance.tmpProxies.TryGetValue(behaviour, out var info))
				{
					instance.tmpProxies.Remove(behaviour);
					info.Dispose();
					return true;
				}
				return false;
			}

			private static void InitInstance()
			{
				if(instance == null)
				{
					var scene = EditorSceneManager.NewPreviewScene();
					var obj = new GameObject("TMP");
					obj.hideFlags = SERVICE_OBJECT_FLAGS;
					EditorSceneManager.MoveGameObjectToScene(obj, scene);
					instance = obj.AddComponent<TmpContainer>();
				}
			}

			private class TmpProxy : IDisposable
			{
				public readonly MonoBehaviour proxy;

				private UdonBehaviour behaviour;
				private TmpContainer container;
				private IDisposable trackerHandle;

				public TmpProxy(MonoBehaviour proxy, UdonBehaviour behaviour, TmpContainer container)
				{
					this.proxy = proxy;
					this.behaviour = behaviour;
					this.container = container;
					trackerHandle = TrackObject(proxy, OnChanged);
				}

				public void Dispose()
				{
					if(proxy != null)
					{
						GameObject.DestroyImmediate(proxy);
						trackerHandle.Dispose();
					}
				}

				private void OnChanged(UnityEngine.Object obj)
				{
					if(obj == null)
					{
						container.tmpProxies.Remove(behaviour);
					}
					else
					{
						ProxyUtils.CopyFieldsToBehaviour(proxy, behaviour);
					}
				}
			}
		}
	}
}