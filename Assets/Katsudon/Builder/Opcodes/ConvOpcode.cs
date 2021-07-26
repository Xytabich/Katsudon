using System;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class ConvOpcode : IOperationBuider
	{
		public int order => 0;

		private Type type;
		private NumericConvertersList convertersList;

		private ConvOpcode(Type type, NumericConvertersList convertersList)
		{
			this.type = type;
			this.convertersList = convertersList;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var variable = method.PeekStack(0);
			if(variable.type == type) return true;
			if(convertersList.TryConvert(method, variable, type, out var converted))
			{
				method.PopStack();
				method.PushStack(converted);
				return true;
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var convertersList = modules.GetModule<NumericConvertersList>();
			var builder = new ConvOpcode(typeof(sbyte), convertersList);
			container.RegisterOpBuilder(OpCodes.Conv_I1, builder);

			builder = new ConvOpcode(typeof(short), convertersList);
			container.RegisterOpBuilder(OpCodes.Conv_I2, builder);

			builder = new ConvOpcode(typeof(int), convertersList);
			container.RegisterOpBuilder(OpCodes.Conv_I4, builder);

			builder = new ConvOpcode(typeof(long), convertersList);
			container.RegisterOpBuilder(OpCodes.Conv_I, builder);

			builder = new ConvOpcode(typeof(long), convertersList);
			container.RegisterOpBuilder(OpCodes.Conv_I8, builder);

			builder = new ConvOpcode(typeof(byte), convertersList);
			container.RegisterOpBuilder(OpCodes.Conv_U1, builder);

			builder = new ConvOpcode(typeof(ushort), convertersList);
			container.RegisterOpBuilder(OpCodes.Conv_U2, builder);

			builder = new ConvOpcode(typeof(uint), convertersList);
			container.RegisterOpBuilder(OpCodes.Conv_U4, builder);

			builder = new ConvOpcode(typeof(ulong), convertersList);
			container.RegisterOpBuilder(OpCodes.Conv_U8, builder);
		}
	}

	[OperationBuilder]
	public class ConvOvfOpcode : IOperationBuider
	{
		public int order => 0;

		private Type type;

		private ConvOvfOpcode(Type type)
		{
			this.type = type;
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var variable = method.PopStack();
			if(variable.type == type) return true;
			if(variable is IConstVariable constVariable)
			{
				method.PushStack(method.machine.GetConstVariable(Convert.ChangeType(constVariable.value, type)));
			}
			else
			{
				if(variable.type == type) method.PushStack(variable);
				else method.machine.ConvertExtern(variable, type, () => method.GetOrPushOutVariable(type));
			}
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new ConvOvfOpcode(typeof(sbyte));

			builder = new ConvOvfOpcode(typeof(short));
			container.RegisterOpBuilder(OpCodes.Conv_Ovf_I2, builder);
			container.RegisterOpBuilder(OpCodes.Conv_Ovf_I2_Un, builder);

			builder = new ConvOvfOpcode(typeof(int));
			container.RegisterOpBuilder(OpCodes.Conv_Ovf_I4, builder);
			container.RegisterOpBuilder(OpCodes.Conv_Ovf_I4_Un, builder);

			builder = new ConvOvfOpcode(typeof(long));
			container.RegisterOpBuilder(OpCodes.Conv_Ovf_I, builder);
			container.RegisterOpBuilder(OpCodes.Conv_Ovf_I_Un, builder);

			builder = new ConvOvfOpcode(typeof(long));
			container.RegisterOpBuilder(OpCodes.Conv_Ovf_I8, builder);
			container.RegisterOpBuilder(OpCodes.Conv_Ovf_I8_Un, builder);

			builder = new ConvOvfOpcode(typeof(byte));
			container.RegisterOpBuilder(OpCodes.Conv_Ovf_U1, builder);
			container.RegisterOpBuilder(OpCodes.Conv_Ovf_U1_Un, builder);

			builder = new ConvOvfOpcode(typeof(ushort));
			container.RegisterOpBuilder(OpCodes.Conv_U2, builder);
			container.RegisterOpBuilder(OpCodes.Conv_Ovf_U2, builder);
			container.RegisterOpBuilder(OpCodes.Conv_Ovf_U2_Un, builder);

			builder = new ConvOvfOpcode(typeof(uint));
			container.RegisterOpBuilder(OpCodes.Conv_Ovf_U4, builder);
			container.RegisterOpBuilder(OpCodes.Conv_Ovf_U4_Un, builder);

			builder = new ConvOvfOpcode(typeof(ulong));
			container.RegisterOpBuilder(OpCodes.Conv_Ovf_U8, builder);
			container.RegisterOpBuilder(OpCodes.Conv_Ovf_U8_Un, builder);

			builder = new ConvOvfOpcode(typeof(float));
			container.RegisterOpBuilder(OpCodes.Conv_R4, builder);
			container.RegisterOpBuilder(OpCodes.Conv_R_Un, builder);

			builder = new ConvOvfOpcode(typeof(double));
			container.RegisterOpBuilder(OpCodes.Conv_R8, builder);
		}
	}
}