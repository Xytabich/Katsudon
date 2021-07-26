using System;
using System.Reflection;
using Katsudon.Builder;
using Katsudon.Info;
using UnityEngine;

namespace Katsudon.Members
{
	[MemberHandler]
	public class InstanceFieldHandler : IMemberHandler
	{
		int IMemberHandler.order => 0;

		bool IMemberHandler.Process(MemberInfo member, AssembliesInfo assemblies, AsmTypeInfo typeInfo)
		{
			var field = member as FieldInfo;
			if(field.IsStatic) return false;

			var flags = AsmFieldInfo.Flags.None;
			if(field.IsPublic)
			{
				flags |= AsmFieldInfo.Flags.Unique;
			}
			if((field.IsPublic || field.IsDefined(typeof(SerializeField))) && !field.IsDefined(typeof(NonSerializedAttribute)))
			{
				flags |= AsmFieldInfo.Flags.Unique;
				flags |= AsmFieldInfo.Flags.Export;
			}
			var syncMode = field.IsDefined(typeof(SyncAttribute)) ? field.GetCustomAttribute<SyncAttribute>().mode : SyncMode.NotSynced;
			if(syncMode != SyncMode.NotSynced)
			{
				flags |= AsmFieldInfo.Flags.Sync;
			}
			typeInfo.AddField(new AsmFieldInfo(flags, syncMode, Utils.PrepareFieldName(field), field));
			return true;
		}

		public static void Register(IMemberHandlersRegistry registry)
		{
			registry.RegisterHandler(MemberTypes.Field, new InstanceFieldHandler());
		}
	}
}