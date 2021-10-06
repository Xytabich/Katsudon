using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.Helpers;

namespace Katsudon.Builder.Extensions.UdonExtensions
{
	[OperationBuilder]
	public class NewobjExternOpcode : IOperationBuider
	{
		public int order => 0;

		private IReadOnlyDictionary<MethodIdentifier, string> externs = null;
		private List<VariableMeta> arguments = new List<VariableMeta>();

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			if(externs == null) externs = UdonCacheHelper.cache.GetCtorNames();

			var ctorInfo = (ConstructorInfo)method.currentOp.argument;
			if(externs.TryGetValue(UdonCacheHelper.cache.GetCtorIdentifier(ctorInfo), out string fullName))
			{
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

				method.machine.AddExtern(fullName, () => method.GetOrPushOutVariable(ctorInfo.DeclaringType), arguments.ToArray());
				arguments.Clear();
				return true;
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new NewobjExternOpcode();
			container.RegisterOpBuilder(OpCodes.Newobj, builder);
		}
	}
}