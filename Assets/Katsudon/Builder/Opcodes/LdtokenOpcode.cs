using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class LdtokenOpcode : IOperationBuider
	{
		public int order => 0;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			if(method.currentOp.argument is Type type)
			{
				var handle = method.GetStateHandle();
				if(handle.Next())// skip Type.GetTypeFromHandle
				{
					if(method.currentOp.opCode == OpCodes.Call)
					{
						var methodInfo = method.currentOp.argument as MethodInfo;
						if(methodInfo.Name == "GetTypeFromHandle" && methodInfo.DeclaringType == typeof(Type))
						{
							handle.Apply();
						}
						else handle.Drop();
					}
					else handle.Drop();
				}
				else handle.Drop();

				method.PushStack(method.machine.GetConstVariable(type, typeof(Type)));
				return true;
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			container.RegisterOpBuilder(OpCodes.Ldtoken, new LdtokenOpcode());
		}
	}
}