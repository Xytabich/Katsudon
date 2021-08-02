namespace Katsudon.Builder.Externs
{
	public static class DebugExtension
	{
		public static void Log(IUdonMachine machine, IVariable variable)
		{
			machine.AddExtern("UnityEngineDebug.__Log__SystemObject__SystemVoid", variable.OwnType());
		}
	}
}