using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Katsudon.Builder.Extensions.Struct;
using Katsudon.Members;
using Katsudon.Utility;
using UnityEngine;

namespace Katsudon.Info
{
	public class AssembliesInfo
	{
		private const string ASSEMBLIES_FILE = "assemblyCache";
		private const byte CACHE_VERSION = 0;

		private static AssembliesInfo _instance;
		public static AssembliesInfo instance => _instance ?? (_instance = new AssembliesInfo());

		public MembersProcessor processor { get; private set; }

		private Dictionary<Type, Guid> cachedGuids = new Dictionary<Type, Guid>();
		private Dictionary<Type, AsmTypeInfo> types = new Dictionary<Type, AsmTypeInfo>();
		private Dictionary<Type, AsmStructInfo> structs = new Dictionary<Type, AsmStructInfo>();

		private AssembliesInfo()
		{
			try
			{
				using(var reader = FileUtils.TryGetFileReader(ASSEMBLIES_FILE))
				{
					if(reader != null)
					{
						if(reader.ReadByte() != CACHE_VERSION) throw new IOException("Invalid version");
						var udonAssemblies = new Dictionary<string, Assembly>();
						foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
						{
							if(Utils.IsUdonAsm(assembly)) udonAssemblies.Add(assembly.GetName().Name, assembly);
						}
						var guidBytes = new byte[16];
						var count = reader.ReadInt32();
						for(int i = 0; i < count; i++)
						{
							try
							{
								var name = reader.ReadString();
								var size = reader.ReadUInt32();
								if(udonAssemblies.TryGetValue(name, out var assembly))
								{
									var typesCount = reader.ReadInt32();
									for(int j = 0; j < typesCount; j++)
									{
										var typeName = reader.ReadString();
										reader.BaseStream.Read(guidBytes, 0, 16);
										var guid = new Guid(guidBytes);
										var type = assembly.GetType(typeName, false, true);
										if(type != null) cachedGuids.Add(type, guid);
									}
								}
								else
								{
									reader.BaseStream.Seek(size, SeekOrigin.Current);
								}
							}
							catch(IOException)
							{
								throw;
							}
							catch(Exception) { }
						}
					}
				}
			}
			catch(IOException)
			{
				FileUtils.DeleteFile(ASSEMBLIES_FILE);
			}
			processor = new MembersProcessor(this);
		}

		public void SaveCache()
		{
			try
			{
				using(var writer = FileUtils.GetFileWriter(ASSEMBLIES_FILE))
				{
					var grouped = new Dictionary<string, List<KeyValuePair<string, Guid>>>();
					foreach(var pair in types)
					{
						var name = pair.Key.Assembly.GetName().Name;
						if(!grouped.TryGetValue(name, out var list))
						{
							list = new List<KeyValuePair<string, Guid>>();
							grouped[name] = list;
						}
						list.Add(new KeyValuePair<string, Guid>(pair.Key.FullName, pair.Value.guid));
					}
					foreach(var pair in structs)
					{
						var name = pair.Key.Assembly.GetName().Name;
						if(!grouped.TryGetValue(name, out var list))
						{
							list = new List<KeyValuePair<string, Guid>>();
							grouped[name] = list;
						}
						list.Add(new KeyValuePair<string, Guid>(pair.Key.FullName, pair.Value.guid));
					}
					writer.Write(CACHE_VERSION);

					var stream = writer.BaseStream;
					writer.Write(grouped.Count);
					foreach(var pair in grouped)
					{
						writer.Write(pair.Key);
						writer.Write((uint)0);
						var start = stream.Position;
						writer.Write(pair.Value.Count);
						foreach(var typeInfo in pair.Value)
						{
							writer.Write(typeInfo.Key);
							writer.Write(typeInfo.Value.ToByteArray());
						}
						var end = stream.Position;
						stream.Seek(start - sizeof(uint), SeekOrigin.Begin);
						writer.Write((uint)(end - start));
						stream.Seek(end, SeekOrigin.Begin);
					}
				}
			}
			catch(Exception e)
			{
				Debug.LogException(e);
			}
		}

		public AsmTypeInfo GetBehaviourInfo(Type type)
		{
			AsmTypeInfo info;
			if(types.TryGetValue(type, out info)) return info;
			if(!Utils.IsUdonAsm(type))
			{
				throw new Exception(string.Format("Behaviour {0} is not supported by Katsudon. The type must be in an assembly marked with the UdonAsm attribute.", type));
			}

			if(!cachedGuids.TryGetValue(type, out var guid))
			{
				guid = Guid.NewGuid();
			}
			if(type.IsInterface)
			{
				info = BuildInterfaceInfo(type, guid);
			}
			else if(type.IsClass && typeof(MonoBehaviour).IsAssignableFrom(type))
			{
				info = BuildBehaviourInfo(type, guid);
			}
			else
			{
				throw new Exception(string.Format("Behaviour {0} is not supported.", type));
			}
			types[type] = info;
			return info;
		}

		public AsmStructInfo GetStructInfo(Type type)
		{
			AsmStructInfo info;
			if(!type.IsClass)
			{
				throw new Exception(string.Format("Struct {0} is not supported by Katsudon. The type is not a class.", type));
			}
			if(structs.TryGetValue(type, out info)) return info;
			if(!Utils.IsUdonAsm(type))
			{
				throw new Exception(string.Format("Struct {0} is not supported by Katsudon. The type must be in an assembly marked with the UdonAsm attribute.", type));
			}
			Type[] interfaces;
			if(type.IsAbstract || type.BaseType != typeof(object) || (interfaces = type.GetInterfaces()).Length > 1 ||
				interfaces.Length == 1 && !typeof(ISerializationCallbackReceiver).IsAssignableFrom(interfaces[0]))
			{
				throw new Exception(string.Format("Struct {0} is not supported by Katsudon. A type cannot be abstract or static, it cannot inherit from other classes or implement interfaces.", type));
			}

			if(!cachedGuids.TryGetValue(type, out var guid))
			{
				guid = Guid.NewGuid();
			}
			bool isSerializable = type.IsDefined(typeof(SerializableAttribute));
			info = new AsmStructInfo(type, guid, isSerializable);
			processor.ProcessStructMembers(type, info);
			structs[type] = info;
			return info;
		}

		public AsmFieldInfo GetField(Type targetType, FieldInfo field)
		{
			if(!typeof(MonoBehaviour).IsAssignableFrom(targetType) || !targetType.Assembly.IsDefined(typeof(UdonAsmAttribute), false))
			{
				return null;
			}
			return GetBehaviourInfo(targetType).GetField(field);
		}

		public AsmMethodInfo GetMethod(Type targetType, MethodInfo method)
		{
			if(!(targetType.IsInterface || typeof(MonoBehaviour).IsAssignableFrom(targetType)) ||
				!targetType.Assembly.IsDefined(typeof(UdonAsmAttribute), false))
			{
				return null;
			}
			return GetBehaviourInfo(targetType).GetMethod(method);
		}

		private AsmTypeInfo BuildInterfaceInfo(Type type, Guid guid)
		{
			var inherits = new HashSet<AsmTypeInfo>();
			foreach(var interfaceType in type.GetInterfaces())
			{
				if(typeof(ISerializationCallbackReceiver).IsAssignableFrom(interfaceType)) continue;
				var interfaceInfo = GetBehaviourInfo(interfaceType);
				inherits.Add(interfaceInfo);
				inherits.UnionWith(interfaceInfo.GetInheritance());
			}

			var types = inherits.ToArray();
			var info = new AsmTypeInfo(type, guid, types, types);
			var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
			for(var i = 0; i < methods.Length; i++)
			{
				info.AddMethod(new AsmMethodInfo(AsmMethodInfo.Flags.Export | AsmMethodInfo.Flags.Unique,
					Utils.PrepareInterfaceMethodName(methods[i]), methods[i]));
			}
			return info;
		}

		private AsmTypeInfo BuildBehaviourInfo(Type type, Guid guid)
		{
			var hierarchy = new List<AsmTypeInfo>();
			var inherits = new HashSet<AsmTypeInfo>();
			foreach(var interfaceType in type.GetInterfaces())
			{
				if(typeof(ISerializationCallbackReceiver).IsAssignableFrom(interfaceType)) continue;
				var interfaceInfo = GetBehaviourInfo(interfaceType);
				inherits.Add(interfaceInfo);
				inherits.UnionWith(interfaceInfo.GetInheritance());
			}

			if(type.BaseType != typeof(MonoBehaviour))
			{
				var baseInfo = GetBehaviourInfo(type.BaseType);
				inherits.Add(baseInfo);
				inherits.UnionWith(baseInfo.GetInheritance());

				hierarchy.AddRange(baseInfo.GetClassHierarchy());
				hierarchy.Add(baseInfo);
			}

			var info = new AsmTypeInfo(type, guid, inherits.ToArray(), hierarchy.ToArray());
			processor.ProcessBehaviourMembers(type, info);
			return info;
		}
	}

	public sealed class AsmMethodInfo
	{
		public const string PARAMETER_FORMAT = "{0}__parameter_{1}";

		public readonly Flags flags;
		public readonly string name;
		public readonly string[] parametersName;
		public readonly string returnName = null;

		public readonly MethodInfo method;

		public AsmMethodInfo(Flags flags, string name, MethodInfo method)
		{
			this.name = name;
			this.flags = flags;
			this.method = method;
			var parameters = method.GetParameters();
			this.parametersName = new string[parameters.Length];
			for(var i = 0; i < parameters.Length; i++)
			{
				this.parametersName[i] = string.Format(PARAMETER_FORMAT, name, i);
			}
			if(method.ReturnType != typeof(void))
			{
				this.returnName = string.Format("{0}__return", name);
			}
		}

		public AsmMethodInfo(Flags flags, string name, string[] parameters, string ret, MethodInfo method)
		{
			this.name = name;
			this.flags = flags;
			this.parametersName = parameters;
			this.returnName = ret;
			this.method = method;
		}

		public AsmMethodInfo(string name, string[] parameters, string ret, AsmMethodInfo method)
		{
			this.name = name;
			this.parametersName = parameters;
			this.returnName = ret;
			this.flags = method.flags;
			this.method = method.method;
		}

		public AsmMethodInfo(Flags flags, AsmMethodInfo info, MethodInfo method)
		{
			this.flags = flags;
			this.name = info.name;
			this.parametersName = info.parametersName;
			this.returnName = info.returnName;
			this.method = method;
		}

		public enum Flags
		{
			None,
			Export = 0x01,
			/// <summary>
			/// Unique methods can only use their own name; therefore, there cannot be more than one method with this name in one declaring type.
			/// Moreover, if there is a method with this name in the base class, it will become private.
			/// </summary>
			Unique = 0x02,
			/// <summary>
			/// Family methods can override methods with the same structure from base types, i.e. use method and parameter names.
			/// </summary>
			Family = 0x04
		}
	}

	public sealed class AsmFieldInfo
	{
		public readonly Flags flags;
		public readonly string name;
		public readonly SyncMode syncMode = SyncMode.NotSynced;

		public readonly FieldInfo field;

		public AsmFieldInfo(Flags flags, SyncMode syncMode, string name, FieldInfo field)
		{
			this.name = name;
			this.flags = flags;
			this.syncMode = syncMode;
			this.field = field;
		}

		public AsmFieldInfo(string name, AsmFieldInfo field)
		{
			this.name = name;
			this.flags = field.flags;
			this.syncMode = field.syncMode;
			this.field = field.field;
		}

		public enum Flags
		{
			None,
			Export = 0x01,
			Sync = 0x02,
			/// <summary>
			/// Unique fields can only use their own name; therefore, the entire class cannot have more than one field with this name.
			/// </summary>
			Unique = 0x04,
			/// <summary>
			/// Same as unique but adding variables with the same name, type and flag will not raise errors.
			/// </summary>
			SharedUnique = 0x08
		}
	}
}