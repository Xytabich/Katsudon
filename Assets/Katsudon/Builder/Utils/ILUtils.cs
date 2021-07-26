using System;
using System.Reflection;

namespace Katsudon.Builder
{
	public static class ILUtils
	{
		public static bool TryGetLdarg(Operation op, out int index)
		{
			switch((int)op.opCode.Value)
			{
				case 0x02:
					index = 0;
					return true;
				case 0x03:
					index = 1;
					return true;
				case 0x04:
					index = 2;
					return true;
				case 0x05:
					index = 3;
					return true;
				case 0x0E:
					index = (byte)op.argument;
					return true;
				case 0x0F:
					index = (byte)op.argument;
					return true;
				case 0xFE09:
					index = (short)op.argument;
					return true;
				case 0xFE0A:
					index = (short)op.argument;
					return true;
			}
			index = -1;
			return false;
		}

		public static bool TryGetLdloc(Operation op, out int index)
		{
			switch((int)op.opCode.Value)
			{
				case 0x06:
					index = 0;
					return true;
				case 0x07:
					index = 1;
					return true;
				case 0x08:
					index = 2;
					return true;
				case 0x09:
					index = 3;
					return true;
				case 0x11:
				case 0x12:
				case 0xFE0C:
				case 0xFE0D:
					index = ((LocalVariableInfo)op.argument).LocalIndex;
					return true;
			}
			index = -1;
			return false;
		}

		public static bool TryGetLdfld(Operation op, out FieldInfo field)
		{
			switch((int)op.opCode.Value)
			{
				case 0x7B:
				case 0x7C:
					field = (FieldInfo)op.argument;
					return true;
			}
			field = null;
			return false;
		}

		public static bool TryGetStarg(Operation op, out int index)
		{
			switch((int)op.opCode.Value)
			{
				case 0x10:
					index = (byte)op.argument;
					return true;
				case 0xFE0B:
					index = (short)op.argument;
					return true;
			}
			index = -1;
			return false;
		}

		public static bool TryGetStloc(Operation op, out int index)
		{
			switch((int)op.opCode.Value)
			{
				case 0x0A:
					index = 0;
					return true;
				case 0x0B:
					index = 1;
					return true;
				case 0x0C:
					index = 2;
					return true;
				case 0x0D:
					index = 3;
					return true;
				case 0x13:
				case 0xFE0E:
					index = ((LocalVariableInfo)op.argument).LocalIndex;
					return true;
			}
			index = -1;
			return false;
		}

		public static bool TryGetStfld(Operation op, out FieldInfo field)
		{
			if(op.opCode.Value == 0x7D)
			{
				field = (FieldInfo)op.argument;
				return true;
			}
			field = null;
			return false;
		}

		public static int GetLdc_I4(Operation op)
		{
			switch((int)op.opCode.Value)
			{
				case 0x15://Ldc_I4_M1
					return -1;
				case 0x16://Ldc_I4_0
					return 0;
				case 0x17://Ldc_I4_1
					return 1;
				case 0x18://Ldc_I4_2
					return 2;
				case 0x19://Ldc_I4_3
					return 3;
				case 0x1A://Ldc_I4_4
					return 4;
				case 0x1B://Ldc_I4_5
					return 5;
				case 0x1C://Ldc_I4_6
					return 6;
				case 0x1D://Ldc_I4_7
					return 7;
				case 0x1E://Ldc_I4_8
					return 8;
				default:
					return Convert.ToInt32(op.argument);
			}
		}
	}
}