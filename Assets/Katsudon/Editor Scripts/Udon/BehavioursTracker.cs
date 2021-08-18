using System;
using System.Collections.Generic;
using System.Reflection;
using Katsudon.Utility;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.Udon;

namespace Katsudon.Editor.Udon
{
	internal static class BehavioursTracker
	{
		public const HideFlags SERVICE_OBJECT_FLAGS = HideFlags.DontSave | HideFlags.HideInInspector;

		private static SceneBehavioursContainer sceneContainer = null;
		private static TmpBehavioursContaner tmpContainer = null;
		private static HashSet<Scene> loadedScenes = null;

		internal static IBehavioursContainer GetOrCreateContainer(Scene scene)
		{
			if(!scene.IsValid())
			{
				if(tmpContainer == null) tmpContainer = new TmpBehavioursContaner();
				return tmpContainer;
			}

			if(sceneContainer == null) sceneContainer = new SceneBehavioursContainer();
			PickFromScene(scene);
			return sceneContainer;
		}

		internal static bool TryGetContainer(Scene scene, out IBehavioursContainer container)
		{
			container = null;
			if(!scene.IsValid())
			{
				if(tmpContainer == null) return false;
				container = tmpContainer;
				return true;
			}

			if(sceneContainer == null) return false;
			container = sceneContainer;
			return true;
		}

		[InitializeOnLoadMethod]
		private static void Init()
		{
			for(int i = 0; i < EditorSceneManager.sceneCount; i++)
			{
				var scene = EditorSceneManager.GetSceneAt(i);
				if(EditorSceneManager.IsPreviewScene(scene) && scene.name == "Katsudon-Proxies")
				{
					EditorSceneManager.ClosePreviewScene(scene);
					continue;
				}
				PickFromScene(scene);
			}

			ObjectFactory.componentWasAdded += OnComponentAdded;

			EditorSceneManager.sceneClosed += OnSceneClosed;
			EditorSceneManager.sceneOpened += OnSceneOpened;
			EditorSceneManager.sceneLoaded += OnSceneLoaded;
			EditorSceneManager.activeSceneChangedInEditMode += ActiveSceneChanged;
		}

		private static void ActiveSceneChanged(Scene previousActiveScene, Scene newActiveScene)
		{
			PickFromScene(newActiveScene);
		}

		private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			PickFromScene(scene);
		}

		private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
		{
			if(mode == OpenSceneMode.AdditiveWithoutLoading) return;
			PickFromScene(scene);
		}

		private static void OnSceneClosed(Scene scene)
		{
			if(loadedScenes != null)
			{
				loadedScenes.Remove(scene);
			}
		}

		private static void PickFromScene(Scene scene)
		{
			if(loadedScenes == null || !loadedScenes.Contains(scene))
			{
				if(loadedScenes == null) loadedScenes = new HashSet<Scene>();
				if(sceneContainer == null) sceneContainer = new SceneBehavioursContainer();
				loadedScenes.Add(scene);

				var roots = scene.GetRootGameObjects();
				bool hasContainers = false;
				var containers = new List<ReferencesContainer>();//FIX: cache
				for(int i = 0; i < roots.Length; i++)
				{
					containers.Clear();
					roots[i].GetComponentsInChildren<ReferencesContainer>(true, containers);
					hasContainers |= containers.Count > 0;
					for(int j = containers.Count - 1; j >= 0; j--)
					{
						sceneContainer.PickContainer(containers[j]);
					}
				}

				if(!hasContainers)
				{
					var behaviours = new List<UdonBehaviour>();//FIX: cache
					for(int i = 0; i < roots.Length; i++)
					{
						behaviours.Clear();
						roots[i].GetComponentsInChildren<UdonBehaviour>(true, behaviours);
						for(int j = behaviours.Count - 1; j >= 0; j--)
						{
							var behaviour = behaviours[j];
							if(behaviour.programSource == null)
							{
								var program = ProgramUtils.ProgramFieldGetter(behaviour);
								if(program != null)
								{
									var script = ProgramUtils.GetScriptByProgram(program);
									if(script != null)
									{
										try
										{
											var proxy = ProxyUtils.CreateProxy(behaviour, script);
											sceneContainer.AddBehaviour(behaviour, proxy);
										}
										catch(Exception e)
										{
											Debug.LogException(e, script);
										}
									}
								}
							}
						}
					}
				}
			}
		}

		private static void OnComponentAdded(Component comp)
		{
			if(comp is UdonBehaviour ubeh)
			{
				if(sceneContainer != null)
				{
					var program = ProgramUtils.ProgramFieldGetter(ubeh);
					if(program != null)
					{
						foreach(var item in DragAndDrop.objectReferences)
						{
							if(item is UdonBehaviour beh)
							{
								sceneContainer.RemoveBehaviour(beh);
							}
						}
						var script = ProgramUtils.GetScriptByProgram(program);
						if(script != null) ProxyUtils.GetOrCreateProxy(ubeh, script);
					}
				}
			}
			else if(comp is MonoBehaviour proxy && Utils.IsUdonAsm(proxy.GetType()))
			{
				if(sceneContainer != null && !object.ReferenceEquals(DragAndDrop.GetGenericData("Katsudon.ComponentDrag"), null))
				{
					foreach(var item in DragAndDrop.objectReferences)
					{
						if(item is MonoBehaviour dragProxy)
						{
							sceneContainer.RemoveProxy(dragProxy, true);
						}
					}
				}

				ProxyUtils.GetOrCreateBehaviour(proxy);
			}
		}

		private class TmpBehavioursContaner : IBehavioursContainer
		{
			private Dictionary<UdonBehaviour, MonoBehaviour> behaviours = new Dictionary<UdonBehaviour, MonoBehaviour>();
			private Scene scene;
			private GameObject root;

			public TmpBehavioursContaner()
			{
				scene = EditorSceneManager.NewPreviewScene();
				scene.name = "Katsudon-Proxies";
				root = new GameObject();
				root.SetActive(false);
				root.hideFlags = HideFlags.HideAndDontSave;
				EditorSceneManager.MoveGameObjectToScene(root, scene);
			}

			public MonoBehaviour CreateProxy(UdonBehaviour behaviour, MonoScript script)
			{
				var proxyType = script.GetClass();
				var proxy = (MonoBehaviour)root.AddComponent(proxyType);
				if(proxy == null) throw new NullReferenceException(string.Format("{0} cannot be added as a component", proxyType));

				proxy.enabled = false;
				proxy.hideFlags = SERVICE_OBJECT_FLAGS;
				return proxy;
			}

			public void AddBehaviour(UdonBehaviour behaviour, MonoBehaviour proxy)
			{
				if(behaviours.TryGetValue(behaviour, out var oldProxy))
				{
					if(oldProxy != null && oldProxy != proxy)
					{
						GameObject.DestroyImmediate(oldProxy);
					}
				}
				behaviours[behaviour] = proxy;
			}

			public bool TryGetBehaviour(MonoBehaviour proxy, out UdonBehaviour behaviour)
			{
				throw new NotImplementedException();
			}

			public bool TryGetProxy(UdonBehaviour behaviour, out MonoBehaviour proxy)
			{
				if(behaviours.TryGetValue(behaviour, out proxy))
				{
					if(proxy == null)
					{
						behaviours.Remove(behaviour);
						return false;
					}
					return true;
				}
				return false;
			}

			public void RemoveBehaviour(UdonBehaviour behaviour)
			{
				if(behaviours.TryGetValue(behaviour, out var proxy))
				{
					behaviours.Remove(behaviour);
					if(proxy != null) GameObject.DestroyImmediate(proxy);
				}
			}
		}

		private class SceneBehavioursContainer : IBehavioursContainer
		{
			private static Func<int, int> _objectDirtyIDGetter = null;
			private static Func<int, int> GetObjectDirtyID
			{
				get
				{
					if(_objectDirtyIDGetter == null)
					{
						_objectDirtyIDGetter = (Func<int, int>)Delegate.CreateDelegate(
							typeof(Func<int, int>),
							typeof(EditorUtility).GetMethod("GetDirtyIndex", BindingFlags.NonPublic | BindingFlags.Static)
						);
					}
					return _objectDirtyIDGetter;
				}
			}

			private Dictionary<MonoBehaviour, int> proxies = new Dictionary<MonoBehaviour, int>();
			private Dictionary<UdonBehaviour, int> behaviours = new Dictionary<UdonBehaviour, int>();
			private List<ReferencesContainer> containers = new List<ReferencesContainer>();
			private List<int> freeContainerIds = new List<int>();

			public SceneBehavioursContainer()
			{
				EditorApplication.update += Update;
			}

			public void PickContainer(ReferencesContainer container)
			{
				int id = PickContainerId();
				container.Init(id, OnRemoveContainer, RemoveBehaviour);
				containers[id] = container;

				var iterator = container.EnumeratePairs();
				while(iterator.MoveNext())
				{
					behaviours.Add(iterator.Current.behaviour, id);
					proxies.Add(iterator.Current.proxy, id);
				}
			}

			public MonoBehaviour CreateProxy(UdonBehaviour behaviour, MonoScript script)
			{
				var proxyType = script.GetClass();
				var proxy = (MonoBehaviour)behaviour.gameObject.AddComponent(proxyType);
				if(proxy == null) throw new NullReferenceException(string.Format("{0} cannot be added as a component", proxyType));

				proxy.enabled = false;
				proxy.hideFlags = SERVICE_OBJECT_FLAGS;
				return proxy;
			}

			public void AddBehaviour(UdonBehaviour behaviour, MonoBehaviour proxy)
			{
				var container = GetOrCreateContainer(behaviour.gameObject);
				proxies[proxy] = container.id;
				behaviours[behaviour] = container.id;
				container.AddPair(behaviour, proxy);
			}

			public void RemoveBehaviour(UdonBehaviour behaviour)
			{
				if(behaviours.TryGetValue(behaviour, out var id))
				{
					var pair = containers[id].FindPair(behaviour);
					RemovePairFromContainer(id, pair.behaviour);
					behaviours.Remove(pair.behaviour);
					proxies.Remove(pair.proxy);
					if(pair.proxy != null) Undo.DestroyObjectImmediate(pair.proxy);
				}
			}

			public void RemoveProxy(MonoBehaviour proxy, bool removeBehaviour = false)
			{
				if(proxies.TryGetValue(proxy, out var id))
				{
					var pair = containers[id].FindPair(proxy);
					RemovePairFromContainer(id, pair.behaviour);
					behaviours.Remove(pair.behaviour);
					proxies.Remove(pair.proxy);
					if(removeBehaviour && pair.behaviour != null) Undo.DestroyObjectImmediate(pair.behaviour);
				}
			}

			public bool TryGetProxy(UdonBehaviour behaviour, out MonoBehaviour proxy)
			{
				proxy = null;
				if(behaviours.TryGetValue(behaviour, out var id))
				{
					var container = containers[id];
					var pair = container.FindPair(behaviour);
					if(pair.proxy == null)
					{
						RemovePairFromContainer(id, pair.behaviour);
						behaviours.Remove(pair.behaviour);
						proxies.Remove(pair.proxy);
						return false;
					}
					proxy = pair.proxy;
					return true;
				}
				return false;
			}

			public bool TryGetBehaviour(MonoBehaviour proxy, out UdonBehaviour behaviour)
			{
				behaviour = null;
				if(proxies.TryGetValue(proxy, out var id))
				{
					var container = containers[id];
					var pair = container.FindPair(proxy);
					if(pair.behaviour == null)
					{
						RemovePairFromContainer(id, pair.behaviour);
						behaviours.Remove(pair.behaviour);
						proxies.Remove(pair.proxy);
						return false;
					}
					behaviour = pair.behaviour;
					return true;
				}
				return false;
			}

			private void Update()
			{
				// Unfortunately, this is the most reliable way
				for(int i = containers.Count - 1; i >= 0; i--)
				{
					containers[i]?.UpdateReferences();
				}
			}

			private int PickContainerId()
			{
				int index;
				if(freeContainerIds.Count > 0)
				{
					index = freeContainerIds[freeContainerIds.Count - 1];
					freeContainerIds.RemoveAt(freeContainerIds.Count - 1);
				}
				else
				{
					index = containers.Count;
					containers.Add(null);
				}
				return index;
			}

			private ReferencesContainer GetOrCreateContainer(GameObject obj)
			{
				var container = obj.GetComponent<ReferencesContainer>();
				if(container != null && container.id >= 0) return container;

				if(container == null)
				{
					container = obj.AddComponent<ReferencesContainer>();
					container.hideFlags = SERVICE_OBJECT_FLAGS;
				}
				int id = PickContainerId();
				container.Init(id, OnRemoveContainer, RemoveBehaviour);
				containers[id] = container;
				Undo.ClearUndo(container);
				return container;
			}

			private void RemovePairFromContainer(int id, UdonBehaviour behaviour)
			{
				var container = containers[id];
				container.RemovePair(behaviour);
				if(container.pairsCount < 1)
				{
					containers[id] = null;
					freeContainerIds.Add(id);

					GameObject.DestroyImmediate(container);
					Undo.ClearUndo(container);
				}
			}

			private void OnRemoveContainer(int id)
			{
				var container = containers[id];
				containers[id] = null;
				freeContainerIds.Add(id);

				var iterator = container.EnumeratePairs();
				if(iterator != null)
				{
					while(iterator.MoveNext())
					{
						behaviours.Remove(iterator.Current.behaviour);
						proxies.Remove(iterator.Current.proxy);
					}
				}
			}
		}

		internal interface IBehavioursContainer
		{
			MonoBehaviour CreateProxy(UdonBehaviour behaviour, MonoScript script);

			void AddBehaviour(UdonBehaviour behaviour, MonoBehaviour proxy);

			bool TryGetProxy(UdonBehaviour behaviour, out MonoBehaviour proxy);

			bool TryGetBehaviour(MonoBehaviour proxy, out UdonBehaviour behaviour);

			void RemoveBehaviour(UdonBehaviour behaviour);
		}
	}
}