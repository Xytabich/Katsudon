using VRC.Udon.VM.Common;

namespace Katsudon.Builder
{
	public static class MachineUtility
	{
		public static void AddCopy(UdonMachine udonMachine, IVariable fromVariable, IVariable toVariable)
		{
			udonMachine.AddOpcode(OpCode.PUSH, fromVariable);
			udonMachine.AddOpcode(OpCode.PUSH, toVariable);

			udonMachine.AddOpcode(OpCode.COPY);
		}
	}
}