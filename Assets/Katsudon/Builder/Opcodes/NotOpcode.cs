using System;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class NotOpcode : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var value = method.PopStack();
			var code = Type.GetTypeCode(value.type);
			if(value is IConstVariable constValue)
			{
				switch(code)
				{
					case TypeCode.SByte: method.machine.GetConstVariable(~(sbyte)constValue.value); return true;
					case TypeCode.Byte: method.machine.GetConstVariable(~(byte)constValue.value); return true;
					case TypeCode.Int16: method.machine.GetConstVariable(~(short)constValue.value); return true;
					case TypeCode.UInt16: method.machine.GetConstVariable(~(ushort)constValue.value); return true;
					case TypeCode.Int32: method.machine.GetConstVariable(~(int)constValue.value); return true;
					case TypeCode.UInt32: method.machine.GetConstVariable(~(uint)constValue.value); return true;
					case TypeCode.Int64: method.machine.GetConstVariable(~(long)constValue.value); return true;
					case TypeCode.UInt64: method.machine.GetConstVariable(~(ulong)constValue.value); return true;
				}
			}
			else
			{
				int size = NumberCodeUtils.GetSize(code);
				if(size < 8)
				{
					bool unsigned = NumberCodeUtils.IsUnsigned(code);
					if(size < 4)
					{
						unsigned = false;
						var tmp = method.GetTmpVariable(typeof(int));
						method.machine.ConvertExtern(value, tmp);
						value = tmp;
					}
					AddExtern(method, value, method.machine.GetConstVariable(unsigned ? (object)uint.MaxValue : (object)(int)-1), unsigned ? typeof(uint) : typeof(int));
				}
				else
				{
					AddExtern(method, value, method.machine.GetConstVariable(NumberCodeUtils.IsUnsigned(code) ? (object)ulong.MaxValue : (object)(long)-1), value.type);
				}
			}
			return true;
		}

		private static void AddExtern(IMethodDescriptor method, IVariable value, IVariable constVariable, Type outType)
		{
			method.machine.BinaryOperatorExtern(BinaryOperator.LogicalXor, value, constVariable, outType, () => method.GetOrPushOutVariable(outType));
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new NotOpcode();
			container.RegisterOpBuilder(OpCodes.Not, builder);
		}
	}
}