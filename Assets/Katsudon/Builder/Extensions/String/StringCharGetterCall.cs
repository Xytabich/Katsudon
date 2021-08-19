using System;
using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;

namespace Katsudon.Builder.Extensions.StringExtension
{
	[OperationBuilder]
	public class StringCharGetterCall : IOperationBuider
	{
		int IOperationBuider.order => 30;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = (MethodInfo)method.currentOp.argument;
			if(methodInfo.Name != "get_Chars" || methodInfo.DeclaringType != typeof(String)) return false;

			var index = method.PopStack();
			var str = method.PopStack();

			var tmp = method.GetTmpVariable(typeof(string));
			method.machine.AddExtern("SystemString.__Substring__SystemInt32_SystemInt32__SystemString", tmp,
				str.OwnType(), index.OwnType(), method.machine.GetConstVariable((int)1).OwnType());
			method.machine.ConvertExtern(tmp, typeof(char), () => method.GetOrPushOutVariable(typeof(char)));
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new StringCharGetterCall();
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
		}
	}
}