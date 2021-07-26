using System;
using UnityEngine;
using VRC.Udon;

namespace Katsudon.Builder.Variables
{
	[VariableBuilder]
	public class BehaviourTypes : IVariableBuilder
	{
		int IVariableBuilder.order => 50;

		bool IVariableBuilder.TryBuildVariable(IVariable variable, VariablesTable table)
		{
			var type = variable.type;
			if((type.IsInterface || typeof(MonoBehaviour).IsAssignableFrom(type)) && Utils.IsUdonAsm(type))
			{
				table.AddVariable(TypedVariable.From(variable, typeof(UdonBehaviour)));
				return true;
			}
			return false;
		}

		bool IVariableBuilder.TryConvert(Type type, ref object value)
		{
			if((type.IsInterface || typeof(MonoBehaviour).IsAssignableFrom(type)) && Utils.IsUdonAsm(type))
			{
				throw new InvalidCastException(string.Format("{0} cannot be converted to UdonBehaviour", value));
			}
			return false;
		}

		public static void Register(VariableBuildersCollection container, IModulesContainer modules)
		{
			container.AddBuilder(new BehaviourTypes());
		}
	}
}