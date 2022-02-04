using System;
using System.Collections.Generic;
using System.Reflection;
using Katsudon.Builder.Helpers;

namespace Katsudon.Info
{
	public class AsmTypeInfo
	{
		public const string TYPE_ID_NAME = "__typeID";
		public const string INHERIT_IDS_NAME = "__inheritsTypeIDs";
		public const string INTERNAL_NAME_FORMAT = "__{0}_{1}";

		public readonly Type type;
		public readonly Guid guid;

		public event System.Action<MethodInfo> onMethodRequested;

		private Dictionary<string, NameCounter> methodNamesCounter = null;
		private Dictionary<MethodNameId, AsmMethodInfo> familyMethods = null;
		private Dictionary<string, NameCounter> fieldNamesCounter = null;

		private Dictionary<FieldIdentifier, AsmFieldInfo> fields = new Dictionary<FieldIdentifier, AsmFieldInfo>();
		// Methods can be overridden, for example, if there is a need to hide the base method
		private Dictionary<MethodIdentifier, AsmMethodInfo> methods = new Dictionary<MethodIdentifier, AsmMethodInfo>();

		private AsmTypeInfo[] inherits;
		private AsmTypeInfo[] hierarchy;

		public AsmTypeInfo(Type type, Guid guid, AsmTypeInfo[] inherits, AsmTypeInfo[] hierarchy)
		{
			this.type = type;
			this.guid = guid;
			this.inherits = inherits;
			this.hierarchy = hierarchy;
		}

		public IEnumerable<AsmTypeInfo> GetInheritance()
		{
			return inherits;
		}

		public IEnumerable<AsmTypeInfo> GetClassHierarchy()
		{
			return hierarchy;
		}

		public void AddField(AsmFieldInfo field)
		{
			if((field.flags & AsmFieldInfo.Flags.Unique) != 0)
			{
				SetUniqueFieldName(field.name);
			}
			else
			{
				field = new AsmFieldInfo(PickFieldName(field.name), field);
			}
			fields[UdonCacheHelper.cache.GetFieldIdentifier(field.field)] = field;
		}

		public void AddMethod(AsmMethodInfo method)
		{
			if((method.flags & (AsmMethodInfo.Flags.Family | AsmMethodInfo.Flags.Unique)) != 0)
			{
				bool isNew = true;
				var id = new MethodNameId(UdonCacheHelper.cache, method.method);
				for(var i = hierarchy.Length - 1; i >= 0; i--)
				{
					var list = hierarchy[i].familyMethods;
					if(list != null && list.TryGetValue(id, out var info))
					{
						// Override base method
						AddMethod(new AsmMethodInfo(AsmMethodInfo.Flags.None, method, info.method));
						method = new AsmMethodInfo(method.flags | info.flags, info, method.method);
						isNew = false;
						break;
					}
				}
				if(isNew)
				{
					if((method.flags & AsmMethodInfo.Flags.Unique) != 0)
					{
						SetUniqueMethodName(method.name);
						for(int i = 0; i < method.parametersName.Length; i++)
						{
							SetUniqueFieldName(method.parametersName[i]);
						}
						if(method.returnName != null) SetUniqueFieldName(method.returnName);
					}
					else
					{
						// First method always has a declared name, the rest are named uniquely
						var retName = method.returnName;
						if(retName != null) PickFieldName(retName, false);

						var paramNames = new string[method.parametersName.Length];
						for(int i = 0; i < paramNames.Length; i++)
						{
							paramNames[i] = PickFieldName(method.parametersName[i], false);
						}
						method = new AsmMethodInfo(PickMethodName(method.name, false), paramNames, retName, method);
					}
				}
				(familyMethods ?? (familyMethods = new Dictionary<MethodNameId, AsmMethodInfo>())).Add(id, method);
			}
			else
			{
				var retName = method.returnName;
				if(retName != null) PickFieldName(retName);

				var paramNames = new string[method.parametersName.Length];
				for(int i = 0; i < paramNames.Length; i++)
				{
					paramNames[i] = PickFieldName(method.parametersName[i]);
				}
				method = new AsmMethodInfo(PickMethodName(method.name), paramNames, retName, method);
			}
			methods[UdonCacheHelper.cache.GetMethodIdentifier(method.method)] = method;
		}

		public void CollectMethods(IDictionary<MethodIdentifier, AsmMethodInfo> list)
		{
			for(var i = 0; i < hierarchy.Length; i++)
			{
				hierarchy[i].CollectMethods(list);
			}
			foreach(var pair in methods)
			{
				list[pair.Key] = pair.Value;
			}
		}

		public void CollectFields(IDictionary<FieldIdentifier, AsmFieldInfo> list)
		{
			for(var i = 0; i < hierarchy.Length; i++)
			{
				hierarchy[i].CollectFields(list);
			}
			foreach(var pair in fields)
			{
				list[pair.Key] = pair.Value;
			}
		}

		public void CollectFieldCounters(IDictionary<string, int> list)
		{
			if(fieldNamesCounter != null)
			{
				foreach(var pair in fieldNamesCounter)
				{
					list.Add(pair.Key, pair.Value.count);
				}
			}
			for(var i = hierarchy.Length - 1; i >= 0; i--)
			{
				var counters = hierarchy[i].fieldNamesCounter;
				if(counters != null)
				{
					foreach(var pair in counters)
					{
						if(!list.ContainsKey(pair.Key)) list.Add(pair.Key, pair.Value.count);
					}
				}
			}
		}

		public AsmMethodInfo GetMethod(MethodIdentifier id)
		{
			AsmMethodInfo info = null;
			if(!methods.TryGetValue(id, out info))
			{
				for(var i = hierarchy.Length - 1; i >= 0; i--)
				{
					if(hierarchy[i].methods.TryGetValue(id, out info))
					{
						break;
					}
				}
			}
			if(info != null && onMethodRequested != null) onMethodRequested(info.method);
			return info;
		}

		public AsmMethodInfo GetMethod(MethodInfo method)
		{
			return GetMethod(UdonCacheHelper.cache.GetMethodIdentifier(method));
		}

		public AsmMethodInfo GetFamilyMethod(MethodInfo method)
		{
			var id = new MethodNameId(UdonCacheHelper.cache, method);
			AsmMethodInfo info = null;
			if(familyMethods == null || !familyMethods.TryGetValue(id, out info))
			{
				for(var i = hierarchy.Length - 1; i >= 0; i--)
				{
					var list = hierarchy[i].familyMethods;
					if(list != null && list.TryGetValue(id, out info))
					{
						break;
					}
				}
			}
			if(info != null)
			{
				if(onMethodRequested != null) onMethodRequested(info.method);
				return info;
			}
			return GetMethod(method);
		}

		public AsmFieldInfo GetField(FieldInfo field)
		{
			var id = UdonCacheHelper.cache.GetFieldIdentifier(field);
			AsmFieldInfo info = null;
			if(!fields.TryGetValue(id, out info))
			{
				for(var i = hierarchy.Length - 1; i >= 0; i--)
				{
					if(hierarchy[i].fields.TryGetValue(id, out info)) return info;
				}
			}
			return info;
		}

		public override string ToString()
		{
			return type.ToString();
		}

		private string PickFieldName(string name, bool unique = true)
		{
			int counter = GetCounter(true, name).PickNumber();
			return unique || counter > 0 ? string.Format(INTERNAL_NAME_FORMAT, counter, name) : name;
		}

		private void SetUniqueFieldName(string name)
		{
			if(!GetCounter(true, name).SetUnique())
			{
				throw new Exception(string.Format("The behavior {0} already contains a unique field named '{1}'", type, name));
			}
		}

		private string PickMethodName(string name, bool unique = true)
		{
			int counter = GetCounter(false, name).PickNumber();
			return unique || counter > 0 ? string.Format(INTERNAL_NAME_FORMAT, counter, name) : name;
		}

		private void SetUniqueMethodName(string name)
		{
			if(!GetCounter(false, name).SetUnique())
			{
				throw new Exception(string.Format("The behavior {0} already contains a unique method named '{1}'", type, name));
			}
		}

		private NameCounter GetCounter(bool field, string name)
		{
			if(IsInternalNameFormat(name))
			{
				UnityEngine.Debug.LogWarning(string.Format("The member '{0}' from type {1} uses an internal naming format, which can lead to compilation failures.", name, type));
			}
			var container = field ? fieldNamesCounter : methodNamesCounter;
			if(container == null)
			{
				container = new Dictionary<string, NameCounter>();
				if(field) fieldNamesCounter = container;
				else methodNamesCounter = container;
			}
			if(!container.TryGetValue(name, out var counter))
			{
				container[name] = counter = new NameCounter(0, false);
				for(var i = hierarchy.Length - 1; i >= 0; i--)
				{
					var list = field ? hierarchy[i].fieldNamesCounter : hierarchy[i].methodNamesCounter;
					if(list != null && list.TryGetValue(name, out var c))
					{
						counter.CopyFrom(c);
						break;
					}
				}
			}
			return counter;
		}

		private static bool IsInternalNameFormat(string str)
		{
			if(str.Length > 3 && str.StartsWith("__") && char.IsDigit(str[2]))
			{
				for(int i = 3; i < str.Length; i++)
				{
					if(!char.IsDigit(str[i]))
					{
						if(str[i] == '_')
						{
							return true;
						}
					}
				}
			}
			return false;
		}

		private class NameCounter
		{
			public int count => counter;
			public bool isUnique => hasUnique;

			private int counter;
			private bool hasUnique;

			public NameCounter(int counter, bool hasUnique)
			{
				this.counter = counter;
				this.hasUnique = hasUnique;
			}

			public void CopyFrom(NameCounter c)
			{
				this.counter = c.counter;
				this.hasUnique = c.hasUnique;
			}

			public int PickNumber()
			{
				return counter++;
			}

			public bool SetUnique()
			{
				if(hasUnique) return false;
				hasUnique = true;
				return true;
			}
		}

		public struct MethodNameId : IEquatable<MethodNameId>
		{
			private string name;
			private int[] arguments;
			private int hashCode;

			public MethodNameId(IUdonPartsCache cache, MethodBase info)
			{
				name = info.Name;

				var parameters = info.GetParameters();
				arguments = new int[parameters.Length];
				for(var i = 0; i < parameters.Length; i++)
				{
					var type = parameters[i].ParameterType;
					if(type.IsByRef) type = type.GetElementType();
					arguments[i] = cache.GetTypeIdentifier(type);
				}

				hashCode = -890188069;
				hashCode = hashCode * -1521134295 + name.GetHashCode();
				for(var i = 0; i < arguments.Length; i++)
				{
					hashCode = hashCode * -1521134295 + arguments[i].GetHashCode();
				}
			}

			public override bool Equals(object obj)
			{
				return obj is MethodIdentifier identifier && Equals(identifier);
			}

			public bool Equals(MethodNameId other)
			{
				if(name == other.name)
				{
					var args = other.arguments;
					int len = arguments.Length;
					if(len == args.Length)
					{
						if(len > 0)
						{
							unsafe
							{
								fixed(int* a = arguments, b = args)
								{
									for(var i = 0; i < len; i++)
									{
										if(a[i] != b[i]) return false;
									}
								}
							}
						}
						return true;
					}
				}
				return false;
			}

			public override int GetHashCode()
			{
				return hashCode;
			}

			public static bool operator ==(MethodNameId a, MethodNameId b)
			{
				return a.Equals(b);
			}

			public static bool operator !=(MethodNameId a, MethodNameId b)
			{
				return !a.Equals(b);
			}
		}
	}
}