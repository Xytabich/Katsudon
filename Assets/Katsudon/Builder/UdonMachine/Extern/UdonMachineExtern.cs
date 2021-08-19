using System;

namespace Katsudon.Builder.Externs
{
	public static class UdonMachineExtension
	{
		public static void SetVariableExtern(this IUdonMachine machine, IVariable targetVariable, string name, IVariable valueVariable)
		{
			machine.AddExtern(
				"VRCUdonCommonInterfacesIUdonEventReceiver.__SetProgramVariable__SystemString_SystemObject__SystemVoid",
				targetVariable.OwnType(), machine.GetConstVariable(name).OwnType(), valueVariable.OwnType()
			);
		}

		public static void SetVariableExtern(this IUdonMachine machine, IVariable targetVariable, IVariable name, IVariable valueVariable)
		{
			machine.AddExtern(
				"VRCUdonCommonInterfacesIUdonEventReceiver.__SetProgramVariable__SystemString_SystemObject__SystemVoid",
				targetVariable.OwnType(), name.OwnType(), valueVariable.OwnType()
			);
		}

		public static void SetVariableExtern(this IUdonMachine machine, IVariable targetVariable, string name, VariableMeta valueVariable)
		{
			machine.AddExtern(
				"VRCUdonCommonInterfacesIUdonEventReceiver.__SetProgramVariable__SystemString_SystemObject__SystemVoid",
				targetVariable.OwnType(), machine.GetConstVariable(name).OwnType(), valueVariable
			);
		}

		public static void SetVariableExtern(this IUdonMachine machine, IVariable targetVariable, IVariable name, VariableMeta valueVariable)
		{
			machine.AddExtern(
				"VRCUdonCommonInterfacesIUdonEventReceiver.__SetProgramVariable__SystemString_SystemObject__SystemVoid",
				targetVariable.OwnType(), name.OwnType(), valueVariable
			);
		}

		public static void GetVariableExtern(this IUdonMachine machine, IVariable targetVariable, string name, Func<IVariable> outVariableCtor)
		{
			machine.AddExtern(
				"VRCUdonCommonInterfacesIUdonEventReceiver.__GetProgramVariable__SystemString__SystemObject",
				outVariableCtor, targetVariable.OwnType(), machine.GetConstVariable(name).OwnType()
			);
		}

		public static void GetVariableExtern(this IUdonMachine machine, IVariable targetVariable, IVariable name, Func<IVariable> outVariableCtor)
		{
			machine.AddExtern(
				"VRCUdonCommonInterfacesIUdonEventReceiver.__GetProgramVariable__SystemString__SystemObject",
				outVariableCtor, targetVariable.OwnType(), name.OwnType()
			);
		}

		public static void GetVariableExtern(this IUdonMachine machine, IVariable targetVariable, IVariable name, IVariable outVariable)
		{
			machine.AddExtern(
				"VRCUdonCommonInterfacesIUdonEventReceiver.__GetProgramVariable__SystemString__SystemObject",
				outVariable, targetVariable.OwnType(), name.OwnType()
			);
		}

		public static void GetVariableExtern(this IUdonMachine machine, IVariable targetVariable, string name, IVariable outVariable)
		{
			machine.AddExtern(
				"VRCUdonCommonInterfacesIUdonEventReceiver.__GetProgramVariable__SystemString__SystemObject",
				outVariable, targetVariable.OwnType(), machine.GetConstVariable(name).OwnType()
			);
		}

		public static void SendEventExtern(this IUdonMachine machine, IVariable targetVariable, string name)
		{
			machine.AddExtern(
				"VRCUdonCommonInterfacesIUdonEventReceiver.__SendCustomEvent__SystemString__SystemVoid",
				targetVariable.OwnType(), machine.GetConstVariable(name).OwnType()
			);
		}

		public static void SendEventExtern(this IUdonMachine machine, IVariable targetVariable, IVariable name)
		{
			machine.AddExtern(
				"VRCUdonCommonInterfacesIUdonEventReceiver.__SendCustomEvent__SystemString__SystemVoid",
				targetVariable.OwnType(), name.OwnType()
			);
		}
	}
}