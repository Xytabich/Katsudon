using System;
using System.Collections.Generic;
using System.Reflection;
using Katsudon.Builder;
using Katsudon.Builder.Extensions.Struct;
using Katsudon.Info;
using Katsudon.Utility;
using UnityEngine.Assertions;

namespace Katsudon.Members
{
	public class MembersProcessor : IComparer<IMemberHandler>, IMemberHandlersRegistry
	{
		private AssembliesInfo assemblies;
		private Dictionary<MemberTypes, SortedSet<IMemberHandler>> builders = new Dictionary<MemberTypes, SortedSet<IMemberHandler>>();

		public MembersProcessor(AssembliesInfo assemblies)
		{
			this.assemblies = assemblies;
			var sortedTypes = OrderedTypeUtils.GetOrderedSet<MemberHandlerAttribute>();
			var args = new object[] { this };
			foreach(var pair in sortedTypes)
			{
				var method = MethodSearch<MemberHandlerDelegate>.FindStaticMethod(pair.Value, "Register");
				Assert.IsNotNull(method, string.Format("Member handler with type {0} does not have a Register method", pair.Value));
				method.Invoke(null, args);
			}
		}

		public void RegisterHandler(MemberTypes type, IMemberHandler builder)
		{
			SortedSet<IMemberHandler> list;
			if(!builders.TryGetValue(type, out list))
			{
				list = new SortedSet<IMemberHandler>(this);
				builders[type] = list;
			}
			list.Add(builder);
		}

		public void UnRegisterHandler(MemberTypes type, IMemberHandler builder)
		{
			SortedSet<IMemberHandler> list;
			if(builders.TryGetValue(type, out list))
			{
				list.Remove(builder);
			}
		}

		public void ProcessBehaviourMembers(Type type, AsmTypeInfo info)
		{
			const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
			ProcessMembers(type.GetFields(flags), MemberTypes.Field, info);
			ProcessMembers(type.GetProperties(flags), MemberTypes.Property, info);
			ProcessMembers(type.GetEvents(flags), MemberTypes.Event, info);
			ProcessMembers(type.GetMethods(flags), MemberTypes.Method, info);
			ProcessMembers(type.GetNestedTypes(flags), MemberTypes.NestedType, info);
		}

		public void ProcessStructMembers(Type type, AsmStructInfo info)
		{
			const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
			if(info.isSerializable)
			{
				var fields = type.GetFields(flags);
				var list = CollectionCache.GetList<KeyValuePair<FieldInfo, int>>();
				int maxIndex = -1;
				foreach(var field in fields)
				{
					if(field.IsStatic) continue;
					var attrib = field.GetCustomAttribute<FieldPositionAttribute>(false);
					if(attrib == null)
					{
						throw new Exception(string.Format("Struct {0} is not supported by Katsudon. Serializable type must explicitly determine the position of the fields using the FieldOffset attribute.", type));
					}
					if(attrib.position < 0)
					{
						throw new Exception(string.Format("Invalid position of field {0} in {1}. Position cannot be less than zero.", field, type));
					}
					maxIndex = Math.Max(maxIndex, attrib.position);
					list.Add(new KeyValuePair<FieldInfo, int>(field, attrib.position));
				}
				var orderedFields = new FieldInfo[maxIndex + 1];
				foreach(var pair in list)
				{
					if(orderedFields[pair.Value] != null)
					{
						throw new Exception(string.Format("Invalid position of field {0} in {1}. A field with this position already exists.", pair.Key, type));
					}
					orderedFields[pair.Value] = pair.Key;
				}
				info.SetFields(orderedFields);
				CollectionCache.Release(list);
			}
			else
			{
				var list = CollectionCache.GetList<FieldInfo>();
				foreach(var field in type.GetFields(flags))
				{
					if(field.IsStatic) continue;
					list.Add(field);
				}
				info.SetFields(list.ToArray());
				CollectionCache.Release(list);
			}
		}

		private void ProcessMembers(MemberInfo[] members, MemberTypes type, AsmTypeInfo info)
		{
			for(var i = 0; i < members.Length; i++)
			{
				if(builders.TryGetValue(type, out var list))
				{
					foreach(var handler in list)
					{
						if(handler.Process(members[i], assemblies, info)) break;
					}
				}
			}
		}

		int IComparer<IMemberHandler>.Compare(IMemberHandler x, IMemberHandler y)
		{
			if(x.order == y.order) return x == y ? 0 : -1;
			return x.order.CompareTo(y.order);
		}
	}

	public interface IMemberHandler
	{
		int order { get; }

		bool Process(MemberInfo member, AssembliesInfo assemblies, AsmTypeInfo typeInfo);
	}

	public interface IMemberHandlersRegistry
	{
		void RegisterHandler(MemberTypes type, IMemberHandler builder);

		void UnRegisterHandler(MemberTypes type, IMemberHandler builder);
	}

	public delegate void MemberHandlerDelegate(IMemberHandlersRegistry registry);

	public sealed class MemberHandlerAttribute : OrderedTypeAttributeBase
	{
		public MemberHandlerAttribute(int registerOrder = 0) : base(registerOrder) { }
	}
}