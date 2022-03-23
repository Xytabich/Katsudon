using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.Helpers;

namespace Katsudon.Builder.Extensions.UdonExtensions
{
	[OperationBuilder]
	public class CallStructCtorExtern : IOperationBuider
	{
		public int order => 0;

		private IReadOnlyDictionary<MethodIdentifier, string> externs = null;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			if(method.currentOp.argument is ConstructorInfo ctorInfo)
			{
				if(externs == null) externs = UdonCacheHelper.cache.GetCtorNames();

				if(externs.TryGetValue(UdonCacheHelper.cache.GetCtorIdentifier(ctorInfo), out string fullName))
				{
					var arguments = CollectionCache.GetList<VariableMeta>();
					var parameters = ctorInfo.GetParameters();
					if(parameters.Length > 0)
					{
						var iterator = method.PopMultiple(parameters.Length);
						int index = 0;
						while(iterator.MoveNext())
						{
							var parameter = parameters[index].ParameterType;
							arguments.Add(iterator.Current.OwnType());
							index++;
						}
					}
					var target = method.PopStack();
					method.machine.AddExtern(fullName, target, arguments.ToArray());
					target.Use();
					CollectionCache.Release(arguments);
					return true;
				}
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new CallStructCtorExtern();
			container.RegisterOpBuilder(OpCodes.Call, builder);
		}
	}
}