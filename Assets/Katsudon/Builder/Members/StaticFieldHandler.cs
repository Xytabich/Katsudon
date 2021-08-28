using System;
using System.Reflection;
using Katsudon.Info;

namespace Katsudon.Members
{
	[MemberHandler]
	public class StaticFieldHandler : IMemberHandler
	{
		int IMemberHandler.order => -1;

		bool IMemberHandler.Process(MemberInfo member, AssembliesInfo assemblies, AsmTypeInfo typeInfo)
		{
			var field = member as FieldInfo;
			if(field.IsStatic && (field.IsInitOnly || !field.IsLiteral))//keep const
			{
				throw new Exception("Static fields is not supported currently: " + field);
			}
			return false;
		}

		public static void Register(IMemberHandlersRegistry registry)
		{
			registry.RegisterHandler(MemberTypes.Field, new StaticFieldHandler());
		}
	}
}