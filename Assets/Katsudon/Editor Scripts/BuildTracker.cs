using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using Katsudon.Builder;
using Katsudon.Editor.Udon;
using Katsudon.Info;
using Katsudon.Utility;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Compilation;
using UnityEngine;
using VRC.Core;
using VRC.SDKBase.Editor.BuildPipeline;
using VRC.Udon.ProgramSources;

namespace Katsudon.Editor
{
	public static class BuildTracker
	{
		private static string BUILDED_FILE = "buildedInfo";
		private static byte BUILDED_FILE_VERSION = 0;

		private static string FORCEBUILD_FILE = "forceBuild";
		private static byte FORCEBUILD_FILE_VERSION = 0;

		private static string ERROR_FILE = "buildError";
		private static byte ERROR_FILE_VERSION = 0;

		private static bool buildInProcess = false;
		private static bool rebuildCacheLoaded = false;
		private static bool rebuildRequestSaved = false;
		private static bool rebuildRequested = false;
		private static HashSet<string> rebuildListCached = null;

		[InitializeOnLoadMethod]
		private static void Init()
		{
			EditorApplication.playModeStateChanged += OnPlayModeChanged;
			CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
		}

		private static void OnAssemblyCompiled(string assemblyPath, CompilerMessage[] messages)
		{
			InitRebuild();
			if(!rebuildRequested)
			{
				rebuildListCached.Add(Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assemblyPath)));
				SaveRebuild();
			}
		}

		private static void OnPlayModeChanged(PlayModeStateChange state)
		{
			if(state == PlayModeStateChange.ExitingEditMode)
			{
				if(TryGetBuildError(out var exception))
				{
					Debug.LogException(exception);
					typeof(SceneView).GetMethod("ShowCompileErrorNotification", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[0]);
					EditorApplication.ExitPlaymode();
				}
			}
		}

		[DidReloadScripts]
		private static void OnScriptsReloaded()
		{
			bool rebuild = false;
			GetBuildedInfo(out var cache, out var vrcSdkVersion, out var katsudonVersion);
			if(vrcSdkVersion != SDKClientUtilities.GetSDKVersionDate() || katsudonVersion != KatsudonInfo.VERSION)
			{
				rebuild = true;
			}
			else
			{
				foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
				{
					if(!assembly.IsDynamic && Utils.IsUdonAsm(assembly))
					{
						if(!cache.Contains(assembly.FullName))
						{
							rebuild = true;
							break;
						}
					}
				}
			}
			InitRebuild();
			if(!rebuild)
			{
				if(rebuildRequested)
				{
					rebuild = true;
				}
				else if(rebuildListCached.Count > 0)
				{
					foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
					{
						if(!assembly.IsDynamic && Utils.IsUdonAsm(assembly))
						{
							if(rebuildListCached.Contains(Path.GetFullPath(assembly.Location)))
							{
								rebuild = true;
								break;
							}
						}
					}
					if(rebuild)
					{
						rebuildRequested = true;
						SaveRebuild();
					}
					else
					{
						ClearRebuild();
					}
				}
			}
			if(rebuild) RebuildAssemblies();
		}

		[MenuItem("Katsudon/Force Rebuild")]
		private static void ForceRebuild()
		{
			InitRebuild();
			rebuildRequested = true;
			SaveRebuild();
			RebuildAssemblies();
		}

		private static void RebuildAssemblies()
		{
			if(BuildAssemblies())
			{
				var newCache = new HashSet<string>();
				foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
				{
					if(!assembly.IsDynamic && Utils.IsUdonAsm(assembly))
					{
						newCache.Add(assembly.FullName);
					}
				}
				SaveBuildedInfo(newCache);

				AssembliesInfo.instance.SaveCache();
				ClearRebuild();
			}
		}

		private static bool BuildAssemblies()
		{
			if(buildInProcess) return false;
			buildInProcess = true;

			var startTime = DateTime.Now;
			var operationTime = DateTime.Now;

			EditorUtility.DisplayProgressBar("Katsudon", "Collecting scripts for build", 0f);
			var allScripts = MonoImporter.GetAllRuntimeMonoScripts();
			var options = new List<BuildOption>();
			Dictionary<string, List<MonoScript>> libraries = null;
			for(var i = 0; i < allScripts.Length; i++)
			{
				var behaviour = allScripts[i];
				if(IsValidScript(behaviour))
				{
					if(AssetDatabase.IsMainAsset(behaviour))
					{
						string path = null;
						var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(behaviour));
						if(importer.GetExternalObjectMap().TryGetValue(ProgramUtils.GetMainProgramIdentifier(), out var programAsset) && programAsset != null)
						{
							path = AssetDatabase.GetAssetPath(programAsset);
						}
						else
						{
							path = Path.ChangeExtension(AssetDatabase.GetAssetPath(behaviour), ".UProgram.asset");
						}
						options.Add(new BuildOption(behaviour, path));
						Resources.UnloadAsset(importer);
					}
					else
					{
						AssetDatabase.TryGetGUIDAndLocalFileIdentifier(behaviour, out string guid, out long fileId);
						if(libraries == null) libraries = new Dictionary<string, List<MonoScript>>();
						if(!libraries.TryGetValue(guid, out var list)) libraries[guid] = (list = new List<MonoScript>());
						list.Add(behaviour);
					}
				}
			}

			var collectingTime = (DateTime.Now - operationTime);
			operationTime = DateTime.Now;
			EditorUtility.DisplayProgressBar("Katsudon", "Collecting scripts for build", 0.5f);

			List<LibraryInfo> librariesBuild = null;
			if(libraries != null)
			{
				librariesBuild = new List<LibraryInfo>(libraries.Count);
				AssetDatabase.StartAssetEditing();
				foreach(var lib in libraries)
				{
					var path = AssetDatabase.GUIDToAssetPath(lib.Key);
					var importer = AssetImporter.GetAtPath(path);
					var map = importer.GetExternalObjectMap();
					var assetsRemap = CollectionCache.GetDictionary<MonoScript, SerializedUdonProgramAsset>();
					var keys = CollectionCache.GetList<AssetImporter.SourceAssetIdentifier>(map.Keys);
					for(int i = keys.Count - 1; i >= 0; i--)
					{
						var key = keys[i];
						if(key.type == typeof(SerializedUdonProgramAsset) && key.name.StartsWith("Katsudon-"))
						{
							var programAsset = map[key];
							if(programAsset == null)
							{
								map.Remove(key);
								importer.RemoveRemap(key);
							}
							else
							{
								var programImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(programAsset));
								if(programImporter.GetExternalObjectMap().TryGetValue(ProgramUtils.GetScriptIdentifier(), out var scriptAsset))
								{
									if(scriptAsset == null)
									{
										map.Remove(key);
										importer.RemoveRemap(key);
										//TODO: delete program asset?
									}
									else
									{
										if(key.name != ProgramUtils.GetSubProgramIdentifier((scriptAsset as MonoScript).GetClass()).name)
										{
											map.Remove(key);
											importer.RemoveRemap(key);
											assetsRemap.Add(scriptAsset as MonoScript, programAsset as SerializedUdonProgramAsset);
										}
									}
								}
								else
								{
									map.Remove(key);
									importer.RemoveRemap(key);
								}
								Resources.UnloadAsset(programImporter);
							}
						}
					}
					CollectionCache.Release(keys);
					AssetDatabase.WriteImportSettingsIfDirty(path);
					Resources.UnloadAsset(importer);

					var list = lib.Value;
					var buildOptions = new BuildOption[list.Count];
					for(int i = list.Count - 1; i >= 0; i--)
					{
						var behaviour = list[i];
						string programPath;
						if(map.TryGetValue(ProgramUtils.GetSubProgramIdentifier(behaviour.GetClass()), out var programAsset))
						{
							programPath = AssetDatabase.GetAssetPath(programAsset);
						}
						else if(assetsRemap.TryGetValue(behaviour, out var program))
						{
							programPath = AssetDatabase.GetAssetPath(program);
						}
						else
						{
							programPath = Path.ChangeExtension(AssetDatabase.GetAssetPath(behaviour), "." + behaviour.GetClass().ToString() + ".UProgram.asset");
						}

						buildOptions[i] = new BuildOption(list[i], programPath);
					}
					CollectionCache.Release(assetsRemap);
					librariesBuild.Add(new LibraryInfo(path, buildOptions));
				}
				AssetDatabase.StopAssetEditing();
				libraries = null;
			}

			GC.Collect();
			Resources.UnloadUnusedAssets();
			var refreshingTime = (DateTime.Now - operationTime);
			operationTime = DateTime.Now;

			if(options.Count == 0 && (librariesBuild == null || librariesBuild.Count == 0))
			{
				EditorUtility.ClearProgressBar();
				buildInProcess = false;
				return false;
			}

			bool buildError = false;
			AssetDatabase.StartAssetEditing();
			try
			{
				using(var builder = new AssembliesBuilder())
				{
					float step = 1f / options.Count;
					for(int i = options.Count - 1; i >= 0; i--)
					{
						EditorUtility.DisplayProgressBar("Katsudon", "Building scripts", 1f - i * step);
						builder.BuildClass(options[i].script.GetClass(), options[i].programOut, GetExecutionOrder(options[i].script));
					}
					if(librariesBuild != null)
					{
						int count = librariesBuild.Count;
						for(int i = count - 1; i >= 0; i--)
						{
							var buildOptions = librariesBuild[i].options;
							step = 1f / buildOptions.Length;
							string text = string.Format("Building ({0}/{1}): {2}", count - i, count, Path.GetFileName(librariesBuild[i].path));
							for(int j = 0; j < buildOptions.Length; j++)
							{
								EditorUtility.DisplayProgressBar("Katsudon", text, 1f - j * step);
								builder.BuildClass(buildOptions[j].script.GetClass(), buildOptions[j].programOut, GetExecutionOrder(buildOptions[j].script));
							}
						}
					}
				}
			}
			catch(Exception e)
			{
#if KATSUDON_DEBUG
				Debug.LogError(e.ToString());
#else
				Debug.LogException(e);
#endif
				SaveError(e);
				buildError = true;
			}
			AssetDatabase.StopAssetEditing();
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
			if(buildError)
			{
				EditorUtility.ClearProgressBar();
				buildInProcess = false;
				return false;
			}

			GC.Collect();
			Resources.UnloadUnusedAssets();
			var buildingTime = (DateTime.Now - operationTime);
			operationTime = DateTime.Now;

			EditorUtility.DisplayProgressBar("Katsudon", "Linking", 1f);
			AssetDatabase.StartAssetEditing();
			for(var i = 0; i < options.Count; i++)
			{
				var programPath = options[i].programOut;
				var importer = AssetImporter.GetAtPath(programPath);
				importer.AddRemap(ProgramUtils.GetScriptIdentifier(), options[i].script);
				AssetDatabase.WriteImportSettingsIfDirty(programPath);
				Resources.UnloadAsset(importer);

				if(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(options[i].script, out string guid, out long fileId))
				{
					var scriptPath = AssetDatabase.GUIDToAssetPath(guid);
					importer = AssetImporter.GetAtPath(scriptPath);
					importer.AddRemap(ProgramUtils.GetMainProgramIdentifier(), AssetDatabase.LoadMainAssetAtPath(programPath));
					AssetDatabase.WriteImportSettingsIfDirty(scriptPath);
					Resources.UnloadAsset(importer);
				}
			}
			if(librariesBuild != null)
			{
				for(int i = librariesBuild.Count - 1; i >= 0; i--)
				{
					var libraryPath = librariesBuild[i].path;
					var libraryImporter = AssetImporter.GetAtPath(libraryPath);
					var buildOptions = librariesBuild[i].options;
					for(var j = 0; j < buildOptions.Length; j++)
					{
						var programPath = buildOptions[j].programOut;
						var importer = AssetImporter.GetAtPath(programPath);
						importer.AddRemap(ProgramUtils.GetScriptIdentifier(), buildOptions[j].script);
						AssetDatabase.WriteImportSettingsIfDirty(programPath);
						Resources.UnloadAsset(importer);

						libraryImporter.AddRemap(ProgramUtils.GetSubProgramIdentifier(buildOptions[j].script.GetClass()),
							AssetDatabase.LoadMainAssetAtPath(programPath));
					}
					AssetDatabase.WriteImportSettingsIfDirty(libraryPath);
					Resources.UnloadAsset(libraryImporter);
				}
			}
			AssetDatabase.StopAssetEditing();
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			GC.Collect();
			Resources.UnloadUnusedAssets();
			var linkingTime = (DateTime.Now - operationTime);

			Debug.LogFormat("[Katsudon] Build finished in {0}:\nCollecting: {1}\nRefreshing: {2}\nBuilding & Saving: {3}\nLinking: {4}",
				DateTime.Now - startTime, collectingTime, refreshingTime, buildingTime, linkingTime);

			FileUtils.DeleteFile(ERROR_FILE);

			EditorUtility.ClearProgressBar();
			buildInProcess = false;
			return true;
		}

		public static bool IsValidScript(MonoScript script)
		{
			if(script == null)
			{
				return false;
			}
			if(script.GetClass() == null)
			{
				return false;
			}
			var type = script.GetClass();
			if(type.IsAbstract || type.IsGenericType || !type.IsClass)
			{
				return false;
			}
			if(!Utils.IsUdonAsm(type))
			{
				return false;
			}
			if(!typeof(MonoBehaviour).IsAssignableFrom(script.GetClass()))
			{
				return false;
			}
			if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out var guid, out long _) || string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)))
			{
				return false;
			}
			return true;
		}

		private static int GetExecutionOrder(MonoScript script)
		{
			int order = MonoImporter.GetExecutionOrder(script);
			if(order == 0)
			{
				var attrib = script.GetClass().GetCustomAttribute<DefaultExecutionOrder>();
				if(attrib != null) order = attrib.order;
			}
			return order;
		}

		private static void ClearRebuild()
		{
			rebuildRequested = false;
			rebuildRequestSaved = false;
			rebuildListCached.Clear();
			FileUtils.DeleteFile(FORCEBUILD_FILE);
		}

		private static void SaveRebuild()
		{
			if(rebuildRequestSaved) return;
			try
			{
				using(var writer = FileUtils.GetFileWriter(FORCEBUILD_FILE))
				{
					writer.Write(FORCEBUILD_FILE_VERSION);
					writer.Write(rebuildRequested);
					if(rebuildRequested)
					{
						rebuildRequestSaved = true;
					}
					else
					{
						writer.Write(rebuildListCached.Count);
						foreach(var path in rebuildListCached)
						{
							writer.Write(path);
						}
					}
				}
			}
			catch(Exception e)
			{
				Debug.LogException(e);
			}
		}

		private static void InitRebuild()
		{
			if(rebuildCacheLoaded) return;
			rebuildCacheLoaded = true;

			rebuildRequested = false;
			rebuildListCached = new HashSet<string>();
			try
			{
				using(var reader = FileUtils.TryGetFileReader(FORCEBUILD_FILE))
				{
					if(reader != null)
					{
						if(reader.ReadByte() != FORCEBUILD_FILE_VERSION) throw new IOException("Invalid version");
						rebuildRequested = reader.ReadBoolean();
						if(!rebuildRequested)
						{
							int count = reader.ReadInt32();
							for(int i = 0; i < count; i++)
							{
								rebuildListCached.Add(reader.ReadString());
							}
						}
					}
				}
			}
			catch(IOException)
			{
				rebuildRequested = true;
				FileUtils.DeleteFile(FORCEBUILD_FILE);
			}
		}

		private static void GetBuildedInfo(out HashSet<string> list, out string vrcSdkVersion, out string katsudonVersion)
		{
			list = new HashSet<string>();
			vrcSdkVersion = null;
			katsudonVersion = null;
			try
			{
				using(var reader = FileUtils.TryGetFileReader(BUILDED_FILE))
				{
					if(reader != null)
					{
						if(reader.ReadByte() != BUILDED_FILE_VERSION) throw new IOException("Invalid version");
						vrcSdkVersion = reader.ReadString();
						katsudonVersion = reader.ReadString();
						int count = reader.ReadInt32();
						for(int i = 0; i < count; i++)
						{
							list.Add(reader.ReadString());
						}
					}
				}
			}
			catch(IOException)
			{
				FileUtils.DeleteFile(BUILDED_FILE);
			}
		}

		private static void SaveBuildedInfo(HashSet<string> set)
		{
			try
			{
				using(var writer = FileUtils.GetFileWriter(BUILDED_FILE))
				{
					writer.Write(BUILDED_FILE_VERSION);
					writer.Write(SDKClientUtilities.GetSDKVersionDate());
					writer.Write(KatsudonInfo.VERSION);
					writer.Write(set.Count);
					foreach(var name in set)
					{
						writer.Write(name);
					}
				}
			}
			catch(Exception e)
			{
				Debug.LogException(e);
			}
		}

		private static void SaveError(Exception exception)
		{
			try
			{
				using(var writer = FileUtils.GetFileWriter(ERROR_FILE))
				{
					writer.Write(ERROR_FILE_VERSION);
					new BinaryFormatter().Serialize(writer.BaseStream, exception);
				}
			}
			catch(Exception e)
			{
				Debug.LogException(e);
			}
		}

		private static bool TryGetBuildError(out Exception exception)
		{
			try
			{
				using(var reader = FileUtils.TryGetFileReader(ERROR_FILE))
				{
					if(reader != null)
					{
						if(reader.ReadByte() != ERROR_FILE_VERSION) throw new IOException("Invalid version");
						exception = (Exception)new BinaryFormatter().Deserialize(reader.BaseStream);
						return true;
					}
				}
			}
			catch
			{
				exception = new Exception("Unknown exception thrown during build, try to force rebuild");
				return true;
			}
			exception = null;
			return false;
		}

		private struct BuildOption
		{
			public MonoScript script;
			public string programOut;

			public BuildOption(MonoScript script, string programOut)
			{
				this.script = script;
				this.programOut = programOut;
			}
		}

		private struct LibraryInfo
		{
			public string path;
			public BuildOption[] options;

			public LibraryInfo(string path, BuildOption[] options)
			{
				this.path = path;
				this.options = options;
			}
		}

		private class VRCBuildRequestHandler : IVRCSDKBuildRequestedCallback
		{
			public int callbackOrder => 0;

			public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
			{
				if(requestedBuildType == VRCSDKRequestedBuildType.Scene)
				{
					if(TryGetBuildError(out var exception))
					{
						Debug.LogException(exception);
						EditorUtility.DisplayDialog("Build Aborted", "All compiler errors have to be fixed before you can build a scene!", "Ok");
						return false;
					}
				}
				return true;
			}
		}
	}
}