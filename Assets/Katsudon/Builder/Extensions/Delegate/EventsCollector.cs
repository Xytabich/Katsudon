using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Katsudon.Info;
using Katsudon.Members;

namespace Katsudon.Builder.Extensions.DelegateExtension
{
	[MemberHandler]
	public class EventsCollector : IMemberHandler
	{
		int IMemberHandler.order => 20;

		private HashSet<Type> processedTypes = new HashSet<Type>();
		private HashSet<MemberInfo> generatedMembers = new HashSet<MemberInfo>();

		bool IMemberHandler.Process(MemberInfo member, AssembliesInfo assemblies, AsmTypeInfo typeInfo)
		{
			if(generatedMembers.Contains(member)) return true;
			if(processedTypes.Contains(member.DeclaringType)) return false;

			var events = member.DeclaringType.GetEvents(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			for(int i = 0; i < events.Length; i++)
			{
				if(events[i].AddMethod.GetCustomAttribute<CompilerGeneratedAttribute>() != null)
				{
					generatedMembers.Add(events[i]);
					generatedMembers.Add(events[i].AddMethod);
					generatedMembers.Add(events[i].RemoveMethod);
				}
			}

			processedTypes.Add(member.DeclaringType);
			return generatedMembers.Contains(member);
		}

		public static void Register(IMemberHandlersRegistry registry)
		{
			var collector = new EventsCollector();
			registry.RegisterHandler(MemberTypes.Event, collector);
			registry.RegisterHandler(MemberTypes.Method, collector);
		}
	}
}