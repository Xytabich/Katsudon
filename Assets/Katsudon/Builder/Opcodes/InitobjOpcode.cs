using System;
using System.Reflection.Emit;
using Katsudon.Builder.Helpers;

namespace Katsudon.Builder.AsmOpCodes
{
	[OperationBuilder]
	public class InitobjExternOpcode : IOperationBuider
	{
		public int order => 100;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var type = (Type)method.currentOp.argument;
			if(type.IsValueType)
			{
				if(type.IsPrimitive)
				{
					method.machine.AddCopy(method.machine.GetConstVariable(Convert.ChangeType(0, Type.GetTypeCode(type))), method.PopStack());
					return true;
				}
				else
				{
					var ctorIdentifier = new MethodIdentifier(UdonCacheHelper.cache.GetTypeIdentifier(type), ".ctor", new int[0]);
					if(UdonCacheHelper.cache.GetCtorNames().TryGetValue(ctorIdentifier, out var ctorName))
					{
						method.machine.AddExtern(ctorName, method.PopStack());
						return true;
					}
					else if(Utils.IsUdonType(type))
					{
						method.machine.AddCopy(method.machine.GetConstVariable(Activator.CreateInstance(type)), method.PopStack());
						return true;
					}
				}
			}
			else
			{
				method.machine.AddCopy(method.machine.GetConstVariable(null), method.PopStack());
				return true;
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new InitobjExternOpcode();
			container.RegisterOpBuilder(OpCodes.Initobj, builder);
		}
	}
}