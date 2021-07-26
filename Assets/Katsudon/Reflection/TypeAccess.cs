using System;
using System.Collections.Generic;
using System.Reflection;

namespace Katsudon.Reflection
{
	public class TypeAccess
	{
		private Type type;
		private object instance;

		private Dictionary<string, object> instanceMembersCache = null;
		private Dictionary<string, object> staticMembersCache = null;

		public TypeAccess(Type type, object instance = null)
		{
			this.type = type;
			this.instance = instance;
		}

		public FieldAccess<T> GetInstanceField<T>(string name)
		{
			if(instanceMembersCache == null || !instanceMembersCache.TryGetValue(name, out var member))
			{
				if(instanceMembersCache == null) instanceMembersCache = new Dictionary<string, object>();
				member = new FieldAccess<T>(type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), instance);
				instanceMembersCache[name] = member;
			}
			return (FieldAccess<T>)member;
		}

		public FieldAccess<T> GetStaticField<T>(string name)
		{
			if(staticMembersCache == null || !staticMembersCache.TryGetValue(name, out var member))
			{
				if(staticMembersCache == null) staticMembersCache = new Dictionary<string, object>();

				member = new FieldAccess<T>(type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic), null);
				staticMembersCache[name] = member;
			}
			return (FieldAccess<T>)member;
		}
	}

	public class TypeAccess<T> : TypeAccess
	{
		public TypeAccess(T instance = default(T)) : base(typeof(T), instance) { }
	}
}