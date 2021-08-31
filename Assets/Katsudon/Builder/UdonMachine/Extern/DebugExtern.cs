namespace Katsudon.Builder.Externs
{
	public static class DebugExtension
	{
		public static void DebugLog(this IUdonMachine machine, IVariable variable)
		{
			machine.AddExtern("UnityEngineDebug.__Log__SystemObject__SystemVoid", variable.OwnType());
		}

		public static void DebugLogError(this IUdonMachine machine, string message)
		{
			machine.AddExtern("UnityEngineDebug.__LogError__SystemObject__SystemVoid", machine.GetConstVariable(message).OwnType());
		}
	}
}