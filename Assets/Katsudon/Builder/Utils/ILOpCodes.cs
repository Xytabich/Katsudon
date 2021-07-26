using System.Reflection;
using System.Reflection.Emit;

namespace Katsudon.Builder
{
	public static class ILOpCodes
	{
		private static readonly OpCode[] shortOpcodes;
		private static readonly OpCode[] longOpcodes;

		static ILOpCodes()
		{
			shortOpcodes = new OpCode[0xe1];
			longOpcodes = new OpCode[0x1f];

			var fields = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static);

			foreach(var field in fields)
			{
				var opcode = (OpCode)field.GetValue(null);
				if(opcode.OpCodeType == OpCodeType.Nternal) continue;

				if(opcode.Size == 1) shortOpcodes[opcode.Value] = opcode;
				else longOpcodes[opcode.Value & 0xFF] = opcode;
			}
		}

		public static OpCode GetOpCode(int code)
		{
			return (code & 0xFF00) == 0xFE00 ? longOpcodes[code & 0xFF] : shortOpcodes[code];
		}

		public static int GetOpCodeSize(OpCode opCode, object operand)
		{
			int size = opCode.Size;

			switch(opCode.OperandType)
			{
				case OperandType.InlineSwitch:
					size += (1 + ((int[])operand).Length) * 4;
					break;
				case OperandType.InlineI8:
				case OperandType.InlineR:
					size += 8;
					break;
				case OperandType.InlineBrTarget:
				case OperandType.InlineField:
				case OperandType.InlineI:
				case OperandType.InlineMethod:
				case OperandType.InlineString:
				case OperandType.InlineTok:
				case OperandType.InlineType:
				case OperandType.ShortInlineR:
					size += 4;
					break;
				case OperandType.InlineVar:
					size += 2;
					break;
				case OperandType.ShortInlineBrTarget:
				case OperandType.ShortInlineI:
				case OperandType.ShortInlineVar:
					size += 1;
					break;
			}

			return size;
		}

		public static bool UsesLocalVariable(OpCode opCode)
		{
			int value = opCode.Value;
			return value >= 0x06 && value <= 0x0D || value >= 0x11 && value <= 0x13 || value >= 0xFE0C && value <= 0xFE0E;
		}
	}
}