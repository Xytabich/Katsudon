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
			foreach(var field in type.GetFields(flags))
			{
				info.AddField(field);
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