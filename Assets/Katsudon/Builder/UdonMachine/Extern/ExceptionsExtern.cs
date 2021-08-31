using System;

namespace Katsudon.Builder.Externs
{
	public static class ExceptionsExtension
	{
		public static void ThrowException<T>(this IUdonMachine machine, string message) where T : Exception
		{
			var counter = machine.GetAddressCounter();
			machine.DebugLogError(string.Format("{0}: {1}\n{{KatsudonExceptionInfo:{2}:{3}}}", typeof(T).ToString(), message, machine.typeInfo.guid, counter));
			machine.AddJump(UdonMachine.endProgramAddress);
		}
	}
}