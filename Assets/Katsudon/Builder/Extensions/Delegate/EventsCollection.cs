using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Katsudon.Builder.Extensions.DelegateExtension
{
	[StaticBuilderModule]
	public class EventsCollection
	{
		private Dictionary<MethodInfo, FieldInfo> adders = new Dictionary<MethodInfo, FieldInfo>();
		private Dictionary<MethodInfo, FieldInfo> subtractors = new Dictionary<MethodInfo, FieldInfo>();

		private HashSet<Type> handledTypes = new HashSet<Type>();

		public FieldInfo GetEventField(MethodInfo method, bool isAdd)
		{
			if(method.GetCustomAttribute<CompilerGeneratedAttribute>() == null) return null;
			if((isAdd ? adders : subtractors).TryGetValue(method, out var field)) return field;
			if(handledTypes.Contains(method.DeclaringType)) return null;

			var events = method.DeclaringType.GetEvents(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

			field = null;
			for(int i = 0; i < events.Length; i++)
			{
				if(method == (isAdd ? events[i].AddMethod : events[i].RemoveMethod))
				{
					field = AddEvent(events[i]);
				}
				else if(events[i].AddMethod.GetCustomAttribute<CompilerGeneratedAttribute>() != null)
				{
					AddEvent(events[i]);
				}
			}

			handledTypes.Add(method.DeclaringType);
			return field;
		}

		private FieldInfo AddEvent(EventInfo evt)
		{
			var field = evt.DeclaringType.GetField(evt.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			adders[evt.AddMethod] = field;
			subtractors[evt.RemoveMethod] = field;
			return field;
		}

		public static void Register(IModulesContainer modules)
		{
			modules.AddModule(new EventsCollection());
		}
	}
}