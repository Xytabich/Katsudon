using System;
using System.Collections.Generic;
using System.IO;
using Katsudon.Builder;
using Katsudon.Editor.Udon;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Compilation;
using UnityEngine;
using VRC.Udon.ProgramSources;

namespace Katsudon.Editor
{
	public static class BuildTracker
	{
		private const string FORCEBUILD_FILE = "forcebuild";
		private const string ASSEMBLIES_FILE = "assemblyCache";

		private static HashSet<string> rebuildListCached = null;

		[InitializeOnLoadMethod]
		private static void Init()
		{
			CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
		}

		private static void OnAssemblyCompiled(string assemblyPath, CompilerMessage[] messages)
		{
			AddToRebuild(Path.Combine(Directory.GetCurrentDirectory(), assemblyPath).Replace('\\', '/'));
		}

		[DidReloadScripts]
		private static void OnScriptsReloaded()
		{
			InitRebuildList();
			if(rebuildListCached.Count > 0)
			{
				bool rebuild = false;
				var cache = GetAssemblyCache();
				foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
				{
					if(!assembly.IsDynamic && Utils.IsUdonAsm(assembly))
					{
						var name = assembly.GetName();
						if(!cache.Contains(new AssemblyIdentifier(name.Name, name.Version.ToString())))
						{
							rebuild = true;
							break;
						}
						if(rebuildListCached.Contains(assembly.Location.Replace('\\', '/')))
						{
							rebuild = true;
							break;
						}
					}
				}
				rebuildListCached.Clear();
				DeleteFile(FORCEBUILD_FILE);
				if(rebuild) RebuildAssemblies();
			}
		}

		[MenuItem("Katsudon/Force Rebuild")]
		private static void RebuildAssemblies()
		{
			DeleteFile(ASSEMBLIES_FILE);
			if(BuildAssemblies())
			{
				var newCache = new HashSet<AssemblyIdentifier>();
				foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
				{
					if(!assembly.IsDynamic && Utils.IsUdonAsm(assembly))
					{
						var name = assembly.GetName();
						newCache.Add(new AssemblyIdentifier(name.Name, name.Version.ToString()));
					}
				}
				SaveAssemblyCache(newCache);
			}
		}

		private static bool BuildAssemblies()
		{
			var startTime = DateTime.Now;
			var operationTime = DateTime.Now;

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
					var assetsRemap = new Dictionary<MonoScript, SerializedUdonProgramAsset>();
					var keys = new List<AssetImporter.SourceAssetIdentifier>(map.Keys); //FIX: cache
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
					librariesBuild.Add(new LibraryInfo(path, buildOptions));
				}
				AssetDatabase.StopAssetEditing();
				libraries = null;
			}

			GC.Collect();
			Resources.UnloadUnusedAssets();
			var refreshingTime = (DateTime.Now - operationTime);
			operationTime = DateTime.Now;

			if(options.Count > 0 || libraries != null)
			{
				bool buildError = false;
				AssetDatabase.StartAssetEditing();
				try
				{
					var builder = new AssembliesBuilder();
					for(int i = options.Count - 1; i >= 0; i--)
					{
						builder.BuildClass(options[i].script.GetClass(), options[i].programOut, MonoImporter.GetExecutionOrder(options[i].script));
					}
					if(librariesBuild != null)
					{
						for(int i = librariesBuild.Count - 1; i >= 0; i--)
						{
							var buildOptions = librariesBuild[i].options;
							for(int j = 0; j < buildOptions.Length; j++)
							{
								builder.BuildClass(buildOptions[j].script.GetClass(), buildOptions[j].programOut,
									MonoImporter.GetExecutionOrder(buildOptions[j].script));
							}
						}
					}
				}
				catch(Exception e)
				{
					Debug.LogException(e);
					buildError = true;
				}
				AssetDatabase.StopAssetEditing();
				AssetDatabase.SaveAssets();
				AssetDatabase.Refresh();
				if(buildError) return false;

				GC.Collect();
				Resources.UnloadUnusedAssets();
				var buildingTime = (DateTime.Now - operationTime);
				operationTime = DateTime.Now;

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
						for(var j = 0; j < options.Count; j++)
						{
							var programPath = options[j].programOut;
							var importer = AssetImporter.GetAtPath(programPath);
							importer.AddRemap(ProgramUtils.GetScriptIdentifier(), options[j].script);
							AssetDatabase.WriteImportSettingsIfDirty(programPath);
							Resources.UnloadAsset(importer);

							libraryImporter.AddRemap(ProgramUtils.GetSubProgramIdentifier(options[j].script.GetClass()),
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
				return true;
			}
			return false;
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

		private static void AddToRebuild(string path)
		{
			InitRebuildList();
			if(rebuildListCached.Add(path))
			{
				using(var stream = TryGetFileStream(FORCEBUILD_FILE, FileMode.Create))
				{
					var writer = new BinaryWriter(stream);
					writer.Write(rebuildListCached.Count);
					foreach(var item in rebuildListCached)
					{
						writer.Write(item);
					}
				}
			}
		}

		private static void InitRebuildList()
		{
			if(rebuildListCached == null)
			{
				rebuildListCached = new HashSet<string>();
				var stream = TryGetFileStream(FORCEBUILD_FILE, FileMode.Open);
				if(stream != null)
				{
					using(stream)
					{
						var reader = new BinaryReader(stream);
						int count = reader.ReadInt32();
						for(int i = 0; i < count; i++)
						{
							rebuildListCached.Add(reader.ReadString());
						}
					}
				}
			}
		}

		private static HashSet<AssemblyIdentifier> GetAssemblyCache()
		{
			var set = new HashSet<AssemblyIdentifier>();
			var stream = TryGetFileStream(ASSEMBLIES_FILE, FileMode.Open);
			if(stream != null)
			{
				using(stream)
				{
					var reader = new BinaryReader(stream);
					int count = reader.ReadInt32();
					for(int i = 0; i < count; i++)
					{
						var name = reader.ReadString();
						var version = reader.ReadString();
						set.Add(new AssemblyIdentifier(name, version));
					}
				}
			}
			return set;
		}

		private static void SaveAssemblyCache(HashSet<AssemblyIdentifier> set)
		{
			using(var stream = TryGetFileStream(ASSEMBLIES_FILE, FileMode.Create))
			{
				var writer = new BinaryWriter(stream);
				writer.Write(set.Count);
				foreach(var item in set)
				{
					writer.Write(item.name);
					writer.Write(item.version);
				}
			}
		}

		private static FileStream TryGetFileStream(string filename, FileMode mode)
		{
			string dir = LibraryDirectory();
			string path = Path.Combine(dir, filename);
			if(!File.Exists(path))
			{
				if(mode == FileMode.Open) return null;
				if(!Directory.Exists(dir)) Directory.CreateDirectory(dir);
			}
			return File.Open(path, mode);
		}

		private static void DeleteFile(string filename)
		{
			string path = Path.Combine(LibraryDirectory(), filename);
			if(File.Exists(path)) File.Delete(path);
		}

		private static string LibraryDirectory()
		{
			return Path.Combine(Directory.GetCurrentDirectory(), "Library/Katsudon");
		}

		private struct AssemblyIdentifier
		{
			public readonly string name;
			public readonly string version;

			public AssemblyIdentifier(string name, string version)
			{
				this.name = name;
				this.version = version;
			}

			public override bool Equals(object obj)
			{
				return obj is AssemblyIdentifier identifier &&
					   name == identifier.name &&
					   version == identifier.version;
			}

			public override int GetHashCode()
			{
				int hashCode = 1545369197;
				hashCode = hashCode * -1521134295 + name.GetHashCode();
				hashCode = hashCode * -1521134295 + version.GetHashCode();
				return hashCode;
			}
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
	}
}