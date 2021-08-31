using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Katsudon.Builder.Helpers;

namespace Katsudon.Builder.Exceptions
{
	[OperationBuilder]
	public class MethodNotSupportException : IOperationBuider
	{
		public int order => 10000;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = method.currentOp.argument as MethodInfo;
			var cache = UdonCacheHelper.cache;
			if(cache.GetMethodBaseTypes().TryGetValue(cache.GetMethodIdentifier(methodInfo), out var types))
			{
				var sb = new StringBuilder(128);
				sb.Append("Method ");
				sb.Append(methodInfo);
				sb.AppendLine(" not supported for declared target. Use a supported type from the list:");
				for(int i = 0; i < types.Length; i++)
				{
					sb.AppendLine(types[i].ToString());
				}
				throw new Exception(sb.ToString());
			}
			else
			{
				throw new Exception(string.Format("Method {0} declared in {1} is not supported by udon", methodInfo, methodInfo.DeclaringType));
			}
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new MethodNotSupportException();
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
		}
	}
}