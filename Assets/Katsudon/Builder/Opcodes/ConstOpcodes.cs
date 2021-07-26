using System;
using System.Reflection.Emit;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class StringConst : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			method.PushStack(method.machine.GetConstVariable(method.currentOp.argument));
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			container.RegisterOpBuilder(OpCodes.Ldstr, new StringConst());
		}
	}

	[OperationBuilder]
	public class NullConst : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			method.PushStack(method.machine.GetConstVariable(null));
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			container.RegisterOpBuilder(OpCodes.Ldnull, new NullConst());
		}
	}

	[OperationBuilder]
	public class Int32Const : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			switch(method.currentOp.opCode.Value)
			{
				case 0x15://Ldc_I4_M1
					method.PushStack(method.machine.GetConstVariable(-1));
					break;
				case 0x16://Ldc_I4_0
					method.PushStack(method.machine.GetConstVariable(0));
					break;
				case 0x17://Ldc_I4_1
					method.PushStack(method.machine.GetConstVariable(1));
					break;
				case 0x18://Ldc_I4_2
					method.PushStack(method.machine.GetConstVariable(2));
					break;
				case 0x19://Ldc_I4_3
					method.PushStack(method.machine.GetConstVariable(3));
					break;
				case 0x1A://Ldc_I4_4
					method.PushStack(method.machine.GetConstVariable(4));
					break;
				case 0x1B://Ldc_I4_5
					method.PushStack(method.machine.GetConstVariable(5));
					break;
				case 0x1C://Ldc_I4_6
					method.PushStack(method.machine.GetConstVariable(6));
					break;
				case 0x1D://Ldc_I4_7
					method.PushStack(method.machine.GetConstVariable(7));
					break;
				case 0x1E://Ldc_I4_8
					method.PushStack(method.machine.GetConstVariable(8));
					break;
				default:
					method.PushStack(method.machine.GetConstVariable(method.currentOp.argument));
					break;
			}
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new Int32Const();
			container.RegisterOpBuilder(OpCodes.Ldc_I4, builder);
			container.RegisterOpBuilder(OpCodes.Ldc_I4_S, builder);
			container.RegisterOpBuilder(OpCodes.Ldc_I4_M1, builder);
			container.RegisterOpBuilder(OpCodes.Ldc_I4_0, builder);
			container.RegisterOpBuilder(OpCodes.Ldc_I4_1, builder);
			container.RegisterOpBuilder(OpCodes.Ldc_I4_2, builder);
			container.RegisterOpBuilder(OpCodes.Ldc_I4_3, builder);
			container.RegisterOpBuilder(OpCodes.Ldc_I4_4, builder);
			container.RegisterOpBuilder(OpCodes.Ldc_I4_5, builder);
			container.RegisterOpBuilder(OpCodes.Ldc_I4_6, builder);
			container.RegisterOpBuilder(OpCodes.Ldc_I4_7, builder);
			container.RegisterOpBuilder(OpCodes.Ldc_I4_8, builder);
		}
	}

	[OperationBuilder]
	public class Int64Const : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			method.PushStack(method.machine.GetConstVariable(method.currentOp.argument));
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			container.RegisterOpBuilder(OpCodes.Ldc_I8, new Int64Const());
		}
	}

	[OperationBuilder]
	public class SingleConst : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			method.PushStack(method.machine.GetConstVariable(method.currentOp.argument));
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			container.RegisterOpBuilder(OpCodes.Ldc_R4, new SingleConst());
		}
	}

	[OperationBuilder]
	public class DoubleConst : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			method.PushStack(method.machine.GetConstVariable(method.currentOp.argument));
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			container.RegisterOpBuilder(OpCodes.Ldc_R8, new DoubleConst());
		}
	}
}