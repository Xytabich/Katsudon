using System;
using System.Reflection;
using Katsudon.Info;
using Katsudon.Members;
using VRC.Udon.Common;

namespace Katsudon.Builder.Extensions.UdonExtensions
{
	[MemberHandler]
	public class VariableChangeListenersCollector : IMemberHandler
	{
		int IMemberHandler.order => 14;

		bool IMemberHandler.Process(MemberInfo member, AssembliesInfo assemblies, AsmTypeInfo typeInfo)
		{
			var method = member as MethodInfo;
			if(method.IsStatic || method.IsVirtual || method.IsAbstract) return false;
			var attrib = method.GetCustomAttribute<OnVariableChangedAttribute>();
			if(attrib == null) return false;
			if(method.ReturnType != typeof(void))
			{
				throw new Exception("Variable change listener must be of type void");
			}
			var field = member.DeclaringType.GetField(attrib.fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
			AsmFieldInfo fieldInfo;
			if(field == null || (fieldInfo = typeInfo.GetField(field)) == null)
			{
				throw new Exception("Variable change listener points to unknown variable");
			}
			var parameters = method.GetParameters();
			if(parameters.Length != 1 || parameters[0].ParameterType != field.FieldType)
			{
				throw new Exception("Variable change listener has invalid parameters");
			}

			typeInfo.AddMethod(new AsmMethodInfo(
				AsmMethodInfo.Flags.Export | AsmMethodInfo.Flags.Unique,
				VariableChangedEvent.EVENT_PREFIX + fieldInfo.name,
				new string[] { VariableChangedEvent.OLD_VALUE_PREFIX + fieldInfo.name },
				null, method
			));
			return true;
		}

		public static void Register(IMemberHandlersRegistry registry)
		{
			registry.RegisterHandler(MemberTypes.Method, new VariableChangeListenersCollector());
		}
	}
}