using System;

namespace Katsudon.Builder.Externs
{
	public static class ConvertExtension
	{
		public static void ConvertExtern(this IUdonMachine machine, IVariable fromVariable, IVariable outVariable)
		{
			machine.AddExtern(
				GetExternName(fromVariable.type, outVariable.type),
				outVariable,
				fromVariable.OwnType()
			);
		}

		public static void ConvertExtern(this IUdonMachine machine, IVariable fromVariable, Type outType, Func<IVariable> outVariableCtor)
		{
			machine.AddExtern(
				GetExternName(fromVariable.type, outType),
				outVariableCtor,
				fromVariable.OwnType()
			);
		}

		public static string GetExternName(Type inType, Type outType)
		{
			return Utils.GetExternName(typeof(Convert), "__To" + outType.Name + "__{0}__{1}", inType, outType);
		}
	}
}