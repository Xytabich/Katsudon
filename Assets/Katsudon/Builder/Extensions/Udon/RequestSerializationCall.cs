using System.Reflection;
using System.Reflection.Emit;

namespace Katsudon.Builder.Extensions.UdonExtensions
{
	[OperationBuilder]
	public class RequestSerializationCall : IOperationBuider
	{
		public int order => 15;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = method.currentOp.argument as MethodInfo;
			if(methodInfo.Name == nameof(AbstractCallsHelper.RequestSerialization) && methodInfo.DeclaringType == typeof(AbstractCallsHelper))
			{
				var target = method.PopStack();
				method.machine.AddExtern("VRCUdonCommonInterfacesIUdonEventReceiver.__RequestSerialization__SystemVoid", target.OwnType());
				return true;
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new RequestSerializationCall();
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
		}
	}
}