using System;

namespace Katsudon.Builder.Variables
{
	/// <summary>
	/// Converts one constant number to another bit by bit.
	/// </summary>
	[NumberConverter]
	public class BitwiseNumberConstConverter : IFromNumberConverter
	{
		public int order => 90;

		public bool TryConvert(IMethodDescriptor method, in IVariable variable, Type toType, out IVariable converted)
		{
			if(!(variable is IConstVariable constVariable))
			{
				converted = null;
				return false;
			}
			var toCode = Type.GetTypeCode(toType);
			if(!NumberCodeUtils.IsPrimitive(toCode) || !NumberCodeUtils.IsInteger(toCode))
			{
				converted = null;
				return false;
			}

			if(NumberCodeUtils.IsInteger(toCode))
			{
				var fromCode = Type.GetTypeCode(constVariable.value.GetType());
				object outValue = constVariable.value;
				if(NumberCodeUtils.IsUnsigned(fromCode))
				{
					ulong value = Convert.ToUInt64(constVariable.value);
					switch(toCode)
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
					switch(toCode)
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
				converted = method.machine.GetConstVariable(outValue);
			}
			else
			{
				converted = method.machine.GetConstVariable(Convert.ChangeType(constVariable.value, toType));
			}
			return true;
		}

		public static void Register(NumericConvertersList container, IModulesContainer modules)
		{
			container.AddConverter(new BitwiseNumberConstConverter());
		}
	}
}