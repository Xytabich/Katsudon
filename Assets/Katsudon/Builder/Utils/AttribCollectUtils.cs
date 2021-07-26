using System;
using System.Collections.Generic;
using System.Reflection;

namespace Katsudon.Utility
{
	public static class AttribCollectUtils
	{
		public static Type[] CollectTypes(Type attributeType, bool includeAbstract)
		{
			var list = new List<Type>();
			var assembly = attributeType.Assembly.GetName();
			var assemblies = AppDomain.CurrentDomain.GetAssemblies();
			for(int i = 0; i < assemblies.Length; i++)
			{
				if(AssemblyName.ReferenceMatchesDefinition(assemblies[i].GetName(), assembly) ||
					ContainsAssembly(assemblies[i].GetReferencedAssemblies(), assembly))
				{
					var types = assemblies[i].GetTypes();
					for(int j = 0; j < types.Length; j++)
					{
						var type = types[j];
						if(IsValidType(type, includeAbstract) && type.IsDefined(attributeType, false))
						{
							list.Add(type);
						}
					}
				}
			}
			return list.ToArray();
		}

		public static Type[] CollectTypes(Type attributeType, Type baseType, bool includeAbstract)
		{
			var list = new List<Type>();
			var assembly = attributeType.Assembly.GetName();
			var assemblies = AppDomain.CurrentDomain.GetAssemblies();
			for(int i = 0; i < assemblies.Length; i++)
			{
				if(AssemblyName.ReferenceMatchesDefinition(assemblies[i].GetName(), assembly) ||
					ContainsAssembly(assemblies[i].GetReferencedAssemblies(), assembly))
				{
					var types = assemblies[i].GetTypes();
					for(int j = 0; j < types.Length; j++)
					{
						var type = types[j];
						if(IsValidType(type, includeAbstract) && baseType.IsAssignableFrom(type) && type.IsDefined(attributeType, false))
						{
							list.Add(type);
						}
					}
				}
			}
			return list.ToArray();
		}

		public static KeyValuePair<T, Type>[] CollectTypes<T>(bool includeAbstract) where T : Attribute
		{
			var list = new List<KeyValuePair<T, Type>>();
			var assembly = typeof(T).Assembly.GetName();
			var assemblies = AppDomain.CurrentDomain.GetAssemblies();
			for(int i = 0; i < assemblies.Length; i++)
			{
				if(AssemblyName.ReferenceMatchesDefinition(assemblies[i].GetName(), assembly) ||
					ContainsAssembly(assemblies[i].GetReferencedAssemblies(), assembly))
				{
					var types = assemblies[i].GetTypes();
					for(int j = 0; j < types.Length; j++)
					{
						var type = types[j];
						if(IsValidType(type, includeAbstract))
						{
							var attrib = type.GetCustomAttribute<T>();
							if(attrib != null)
							{
								list.Add(new KeyValuePair<T, Type>(attrib, type));
							}
						}
					}
				}
			}
			return list.ToArray();
		}

		public static KeyValuePair<T, Type>[] CollectTypes<T>(Type baseType, bool includeAbstract) where T : Attribute
		{
			var list = new List<KeyValuePair<T, Type>>();
			var assembly = typeof(T).Assembly.GetName();
			var assemblies = AppDomain.CurrentDomain.GetAssemblies();
			for(int i = 0; i < assemblies.Length; i++)
			{
				if(AssemblyName.ReferenceMatchesDefinition(assemblies[i].GetName(), assembly) ||
					ContainsAssembly(assemblies[i].GetReferencedAssemblies(), assembly))
				{
					var types = assemblies[i].GetTypes();
					for(int j = 0; j < types.Length; j++)
					{
						var type = types[j];
						if(IsValidType(type, includeAbstract) && baseType.IsAssignableFrom(type))
						{
							var attrib = type.GetCustomAttribute<T>();
							if(attrib != null)
							{
								list.Add(new KeyValuePair<T, Type>(attrib, type));
							}
						}
					}
				}
			}
			return list.ToArray();
		}

		public static T[] CollectAttribs<T>(bool includeAbstract) where T : Attribute
		{
			var list = new List<T>();
			var assembly = typeof(T).Assembly.GetName();
			var assemblies = AppDomain.CurrentDomain.GetAssemblies();
			for(int i = 0; i < assemblies.Length; i++)
			{
				if(AssemblyName.ReferenceMatchesDefinition(assemblies[i].GetName(), assembly) ||
					ContainsAssembly(assemblies[i].GetReferencedAssemblies(), assembly))
				{
					var types = assemblies[i].GetTypes();
					for(int j = 0; j < types.Length; j++)
					{
						var type = types[j];
						if(IsValidType(type, includeAbstract))
						{
							var attrib = type.GetCustomAttribute<T>();
							if(attrib != null) list.Add(attrib);
						}
					}
				}
			}
			return list.ToArray();
		}

		private static bool ContainsAssembly(AssemblyName[] assemblies, AssemblyName assembly)
		{
			for(int i = 0; i < assemblies.Length; i++)
			{
				if(AssemblyName.ReferenceMatchesDefinition(assemblies[i], assembly))
				{
					return true;
				}
			}
			return false;
		}

		private static bool IsValidType(Type type, bool canBeAbstract)
		{
			return canBeAbstract || (type.IsValueType || !(type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition));
		}
	}
}