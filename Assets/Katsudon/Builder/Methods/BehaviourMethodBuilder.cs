
using System.Reflection;
using VRC.Udon.VM.Common;

namespace Katsudon.Builder.Methods
{
	public class BehaviourMethodBuilder : ProgramBlock.IMethodBuilder
	{
		public int order => 1000;

		private MethodBodyBuilder bodyBuilder;
		private PrimitiveConvertersList convertersList;

		public BehaviourMethodBuilder(MethodBodyBuilder bodyBuilder, PrimitiveConvertersList convertersList)
		{
			this.bodyBuilder = bodyBuilder;
			this.convertersList = convertersList;
		}

		public bool BuildMethod(MethodInfo method, UBehMethodInfo uBehMethod, UdonMachine udonMachine, PropertiesBlock properties)
		{
			/*
			returnAddress = __method_return;
			__method_return = 0xFFFFFFFF;
			*/
			var returnAddressVariable = new UnnamedSignificantVariable("returnAddress", typeof(uint), UdonMachine.LAST_ALIGNED_ADDRESS);
			properties.AddVariable(returnAddressVariable);

			MachineUtility.AddCopy(udonMachine, udonMachine.GetReturnAddressGlobal(), returnAddressVariable);
			MachineUtility.AddCopy(udonMachine, udonMachine.GetConstVariable(UdonMachine.LAST_ALIGNED_ADDRESS).Used(), udonMachine.GetReturnAddressGlobal());
			var returnLabel = new EmbedAddressLabel();

			var machineBlock = new UdonProgramBlock(udonMachine, convertersList);
			bodyBuilder.Build(method, uBehMethod.arguments, uBehMethod.ret, returnLabel, machineBlock, properties);
			machineBlock.CheckVariables();

			if(uBehMethod.ret != null) properties.AddVariable(uBehMethod.ret);
			foreach(var variable in uBehMethod.arguments)
			{
				properties.AddVariable(variable);
			}
			machineBlock.ApplyProperties(properties);

			udonMachine.ApplyLabel(returnLabel);
			udonMachine.AddOpcode(OpCode.JUMP_INDIRECT, returnAddressVariable);
			return true;
		}
	}
}