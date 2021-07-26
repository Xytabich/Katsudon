using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Katsudon.Builder
{
	public class MethodReader : IEnumerable<Operation>
	{
		private byte[] ilBytes;
		private int index;
		private bool isStatic;
		private object objInstance;
		private Module module;
		private Type[] genericMethodArgs;
		private Type[] genericTypeArgs;
		private ParameterInfo[] parameters;
		private IList<LocalVariableInfo> locals;

		public MethodReader(MethodBase method, object objInstance)
		{
			this.module = method.Module;
			this.isStatic = method.IsStatic;
			this.objInstance = objInstance;

			this.parameters = method.GetParameters();

			if(!(method is ConstructorInfo)) genericMethodArgs = method.GetGenericArguments();
			if(method.DeclaringType != null) genericTypeArgs = method.DeclaringType.GetGenericArguments();

			var body = method.GetMethodBody();
			this.locals = body.LocalVariables;
			this.ilBytes = body.GetILAsByteArray();
		}

		IEnumerator<Operation> IEnumerable<Operation>.GetEnumerator()
		{
			this.index = 0;
			while(index < ilBytes.Length)
			{
				int offset = index;
				OpCode opCode = ReadOpCode();
				object argument, rawArgument;
				ReadOperand(opCode, out argument, out rawArgument);
				yield return new Operation(offset, opCode, argument, rawArgument);
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			throw new NotImplementedException();
		}

		private OpCode ReadOpCode()
		{
			OpCode value;
			CheckCanRead(1);
			if(ilBytes[index] == 0xFE)
			{
				index++;
				CheckCanRead(1);
				value = ILOpCodes.GetOpCode(0xFE00 | ilBytes[index]);
			}
			else
			{
				value = ILOpCodes.GetOpCode(ilBytes[index]);
			}
			index++;
			return value;
		}

		private void ReadOperand(OpCode opCode, out object argument, out object rawArgument)
		{
			switch(opCode.OperandType)
			{
				case OperandType.InlineNone:
					argument = null;
					rawArgument = null;
					return;
				case OperandType.InlineSwitch:
					{
						int length = ReadInt32();
						int baseOffset = index + (4 * length);
						int[] offsets = new int[length];
						int[] branches = new int[length];
						for(int i = 0; i < length; i++) offsets[i] = ReadInt32();
						for(int i = 0; i < length; i++) branches[i] = offsets[i] + baseOffset;
						argument = branches;
						rawArgument = offsets;
					}
					return;
				case OperandType.ShortInlineBrTarget:
					{
						sbyte offset = (sbyte)ReadByte();
						argument = offset + index;
						rawArgument = offset;
					}
					return;
				case OperandType.InlineBrTarget:
					{
						int offset = ReadInt32();
						argument = offset + index;
						rawArgument = offset;
					}
					return;
				case OperandType.ShortInlineI:
					if(opCode == OpCodes.Ldc_I4_S)
					{
						argument = rawArgument = (sbyte)ReadByte();
					}
					else
					{
						argument = rawArgument = ReadByte();
					}
					return;
				case OperandType.InlineI:
					argument = rawArgument = ReadInt32();
					return;
				case OperandType.ShortInlineR:
					argument = rawArgument = ReadSingle();
					return;
				case OperandType.InlineR:
					argument = rawArgument = ReadDouble();
					return;
				case OperandType.InlineI8:
					argument = rawArgument = ReadInt64();
					return;
				case OperandType.InlineSig:
					{
						int token = ReadInt32();
						rawArgument = token;
						argument = module.ResolveSignature(token);
					}
					return;
				case OperandType.InlineString:
					{
						int token = ReadInt32();
						rawArgument = token;
						argument = module.ResolveString(token);
					}
					return;
				case OperandType.InlineTok:
				case OperandType.InlineType:
				case OperandType.InlineMethod:
				case OperandType.InlineField:
					{
						int token = ReadInt32();
						rawArgument = token;
						argument = module.ResolveMember(token, genericTypeArgs, genericMethodArgs);
					}
					return;
				case OperandType.ShortInlineVar:
					{
						byte index = ReadByte();
						rawArgument = index;
						argument = GetVariable(opCode, index);
					}
					return;
				case OperandType.InlineVar:
					{
						short index = ReadInt16();
						rawArgument = index;
						argument = GetVariable(opCode, index);
					}
					return;
				default:
					throw new NotSupportedException();
			}
		}

		private object GetVariable(OpCode opCode, int index)
		{
			if(ILOpCodes.UsesLocalVariable(opCode)) return locals[index];
			if(isStatic) return parameters[index];
			return index == 0 ? objInstance : parameters[index - 1];
		}

		private byte ReadByte()
		{
			CheckCanRead(1);
			return ilBytes[index++];
		}

		private byte[] ReadBytes(int length)
		{
			CheckCanRead(length);
			var value = new byte[length];
			Buffer.BlockCopy(ilBytes, index, value, 0, length);
			index += length;
			return value;
		}

		private short ReadInt16()
		{
			CheckCanRead(2);
			short value = (short)(ilBytes[index]
				| (ilBytes[index + 1] << 8));
			index += 2;
			return value;
		}

		private int ReadInt32()
		{
			CheckCanRead(4);
			int value = ilBytes[index]
				| (ilBytes[index + 1] << 8)
				| (ilBytes[index + 2] << 16)
				| (ilBytes[index + 3] << 24);
			index += 4;
			return value;
		}

		private long ReadInt64()
		{
			CheckCanRead(8);
			uint low = (uint)(ilBytes[index]
				| (ilBytes[index + 1] << 8)
				| (ilBytes[index + 2] << 16)
				| (ilBytes[index + 3] << 24));

			uint high = (uint)(ilBytes[index + 4]
				| (ilBytes[index + 5] << 8)
				| (ilBytes[index + 6] << 16)
				| (ilBytes[index + 7] << 24));

			long value = (((long)high) << 32) | low;
			index += 8;
			return value;
		}

		private float ReadSingle()
		{
			CheckCanRead(4);
			if(!BitConverter.IsLittleEndian)
			{
				var bytes = ReadBytes(4);
				Array.Reverse(bytes);
				return BitConverter.ToSingle(bytes, 0);
			}

			float value = BitConverter.ToSingle(ilBytes, index);
			index += 4;
			return value;
		}

		private double ReadDouble()
		{
			CheckCanRead(8);
			if(!BitConverter.IsLittleEndian)
			{
				var bytes = ReadBytes(8);
				Array.Reverse(bytes);
				return BitConverter.ToDouble(bytes, 0);
			}

			double value = BitConverter.ToDouble(ilBytes, index);
			index += 8;
			return value;
		}

		private void CheckCanRead(int count)
		{
			if(index + count > ilBytes.Length)
				throw new ArgumentOutOfRangeException();
		}
	}

	public struct Operation : IComparable<Operation>
	{
		public readonly int offset;
		public readonly OpCode opCode;
		public readonly object argument;
		public readonly object rawArgument;

		public Operation(int offset, OpCode opCode, object argument, object rawArgument)
		{
			this.offset = offset;
			this.opCode = opCode;
			this.argument = argument;
			this.rawArgument = rawArgument;
		}

		public int CompareTo(Operation other)
		{
			return offset.CompareTo(other.offset);
		}
	}
}