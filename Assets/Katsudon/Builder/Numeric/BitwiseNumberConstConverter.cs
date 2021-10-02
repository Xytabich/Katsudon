using System;

namespace Katsudon.Builder.Variables
{
	/// <summary>
	/// Converts one constant number to another bit by bit.
	/// </summary>
	[PrimitiveConverter]
	public class BitwiseNumberConstConverter : IPrimitiveConverter
	{
		public int order => 90;

		public IVariable TryConvert(IUdonProgramBlock block, in IVariable variable, TypeCode fromPrimitive, TypeCode toPrimitive, Type toType)
		{
			if(fromPrimitive == TypeCode.Object) return null;
			if(!toType.IsPrimitive) return null;
			if(!NumberCodeUtils.IsInteger(toPrimitive)) return null;
			if(!(variable is IConstVariable constVariable)) return null;

			if(NumberCodeUtils.IsInteger(toPrimitive))
			{
				object outValue = constVariable.value;
				if(NumberCodeUtils.IsUnsigned(fromPrimitive))
				{
					ulong value = Convert.ToUInt64(constVariable.value);
					switch(toPrimitive)
					{
						case TypeCode.Byte: outValue = (byte)value; break;
						case TypeCode.SByte: outValue = (sbyte)value; break;
						case TypeCode.Int16: outValue = (short)value; break;
						case TypeCode.UInt16: outValue = (ushort)value; break;
						case TypeCode.Int32: outValue = (int)value; break;
						case TypeCode.UInt32: outValue = (uint)value; break;
						case TypeCode.Int64: outValue = (long)value; break;
						case TypeCode.UInt64: outValue = (ulong)value; break;
					}
				}
				else
				{
					long value = Convert.ToInt64(constVariable.value);
					switch(toPrimitive)
					{
						case TypeCode.Byte: outValue = (byte)value; break;
						case TypeCode.SByte: outValue = (sbyte)value; break;
						case TypeCode.Int16: outValue = (short)value; break;
						case TypeCode.UInt16: outValue = (ushort)value; break;
						case TypeCode.Int32: outValue = (int)value; break;
						case TypeCode.UInt32: outValue = (uint)value; break;
						case TypeCode.Int64: outValue = (long)value; break;
						case TypeCode.UInt64: outValue = (ulong)value; break;
					}
				}
				return block.machine.GetConstVariable(outValue);
			}
			else
			{
				return block.machine.GetConstVariable(Convert.ChangeType(constVariable.value, toType));
			}
		}

		public static void Register(PrimitiveConvertersList container, IModulesContainer modules)
		{
			container.AddConverter(new BitwiseNumberConstConverter());
		}
	}
}