using System;

namespace Katsudon
{
	public static class NumberCodeUtils
	{
		public static TypeCode GetCode(Type type)
		{
			if(type.IsPrimitive) return Type.GetTypeCode(type);
			return TypeCode.Object;
		}

		public static bool IsConvertible(Type type)
		{
			if(typeof(IConvertible).IsAssignableFrom(type))
			{
				return IsPrimitive(Type.GetTypeCode(type));
			}
			return false;
		}

		public static bool IsPrimitive(TypeCode typeCode)
		{
			switch(typeCode)
			{
				case TypeCode.Byte:
				case TypeCode.SByte:
				case TypeCode.Boolean:
				case TypeCode.Int16:
				case TypeCode.UInt16:
				case TypeCode.Char:
				case TypeCode.Int32:
				case TypeCode.UInt32:
				case TypeCode.Single:
				case TypeCode.Int64:
				case TypeCode.UInt64:
				case TypeCode.Double:
					return true;
			}
			return false;
		}

		public static bool IsInteger(TypeCode typeCode)
		{
			switch(typeCode)
			{
				case TypeCode.Byte:
				case TypeCode.SByte:
				case TypeCode.Int16:
				case TypeCode.UInt16:
				case TypeCode.Int32:
				case TypeCode.UInt32:
				case TypeCode.Int64:
				case TypeCode.UInt64:
					return true;
			}
			return false;
		}

		public static bool IsFloat(TypeCode typeCode)
		{
			switch(typeCode)
			{
				case TypeCode.Single:
				case TypeCode.Double:
					return true;
			}
			return false;
		}

		public static bool IsUnsigned(TypeCode typeCode)
		{
			switch(typeCode)
			{
				case TypeCode.Byte:
				case TypeCode.Boolean:
				case TypeCode.Char:
				case TypeCode.UInt16:
				case TypeCode.UInt32:
				case TypeCode.UInt64:
					return true;
			}
			return false;
		}

		public static int GetSize(TypeCode typeCode)
		{
			switch(typeCode)
			{
				case TypeCode.Byte:
				case TypeCode.SByte:
				case TypeCode.Boolean:
					return 1;
				case TypeCode.Int16:
				case TypeCode.UInt16:
				case TypeCode.Char:
					return 2;
				case TypeCode.Int32:
				case TypeCode.UInt32:
				case TypeCode.Single:
					return 4;
				case TypeCode.Int64:
				case TypeCode.UInt64:
				case TypeCode.Double:
					return 8;
			}
			throw new InvalidOperationException("Target is not primitive number");
		}

		public static object GetMinValue(TypeCode typeCode)
		{
			switch(typeCode)
			{
				case TypeCode.Byte: return byte.MinValue;
				case TypeCode.SByte: return sbyte.MinValue;
				case TypeCode.Int16: return short.MinValue;
				case TypeCode.UInt16: return ushort.MinValue;
				case TypeCode.Char: return char.MinValue;
				case TypeCode.Int32: return int.MinValue;
				case TypeCode.UInt32: return uint.MinValue;
				case TypeCode.Single: return float.MinValue;
				case TypeCode.Int64: return long.MinValue;
				case TypeCode.UInt64: return ulong.MinValue;
				case TypeCode.Double: return double.MinValue;
			}
			throw new InvalidOperationException("Target is not primitive number");
		}

		public static object GetMaxValue(TypeCode typeCode)
		{
			switch(typeCode)
			{
				case TypeCode.Byte: return byte.MaxValue;
				case TypeCode.SByte: return sbyte.MaxValue;
				case TypeCode.Int16: return short.MaxValue;
				case TypeCode.UInt16: return ushort.MaxValue;
				case TypeCode.Char: return char.MaxValue;
				case TypeCode.Int32: return int.MaxValue;
				case TypeCode.UInt32: return uint.MaxValue;
				case TypeCode.Single: return float.MaxValue;
				case TypeCode.Int64: return long.MaxValue;
				case TypeCode.UInt64: return ulong.MaxValue;
				case TypeCode.Double: return double.MaxValue;
			}
			throw new InvalidOperationException("Target is not primitive number");
		}

		public static TypeCode ToUnsigned(TypeCode typeCode)
		{
			switch(typeCode)
			{
				case TypeCode.SByte: return TypeCode.Byte;
				case TypeCode.Int16: return TypeCode.UInt16;
				case TypeCode.Int32: return TypeCode.UInt32;
				case TypeCode.Int64: return TypeCode.UInt64;
				default: return typeCode;
			}
		}

		public static TypeCode ToSigned(TypeCode typeCode)
		{
			switch(typeCode)
			{
				case TypeCode.Byte: return TypeCode.SByte;
				case TypeCode.UInt16: return TypeCode.Int16;
				case TypeCode.UInt32: return TypeCode.Int32;
				case TypeCode.UInt64: return TypeCode.Int64;
				default: return typeCode;
			}
		}

		public static Type ToType(TypeCode typeCode)
		{
			switch(typeCode)
			{
				case TypeCode.Byte: return typeof(byte);
				case TypeCode.SByte: return typeof(sbyte);
				case TypeCode.Boolean: return typeof(bool);
				case TypeCode.Int16: return typeof(short);
				case TypeCode.UInt16: return typeof(ushort);
				case TypeCode.Char: return typeof(char);
				case TypeCode.Int32: return typeof(int);
				case TypeCode.UInt32: return typeof(uint);
				case TypeCode.Single: return typeof(float);
				case TypeCode.Int64: return typeof(long);
				case TypeCode.UInt64: return typeof(ulong);
				case TypeCode.Double: return typeof(double);
			}
			throw new InvalidOperationException("Target is not primitive number");
		}
	}
}