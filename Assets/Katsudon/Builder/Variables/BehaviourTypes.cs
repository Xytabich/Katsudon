using System;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace Katsudon.Builder.Variables
{
	[VariableBuilder]
	public class BehaviourTypes : IVariableBuilder
	{
		int IVariableBuilder.order => 50;

		bool IVariableBuilder.TryBuildVariable(IVariable variable, VariablesTable table)
		{
			var type = variable.type;
			if(Utils.IsUdonAsmBehaviourOrInterface(type))
			{
				if(variable is ThisVariable)
				{
					table.AddVariable(new ThisVariableProxy(variable));
				}
				else
				{
					table.AddVariable(TypedVariable.From(variable, typeof(UdonBehaviour)));
				}
				return true;
			}
			return false;
		}

		public static void Register(VariableBuildersCollection container, IModulesContainer modules)
		{
			container.AddBuilder(new BehaviourTypes());
		}

		private class ThisVariableProxy : IVariable, ISelfPointingVariable
		{
			public string name => variable.name;
			public Type type => typeof(UdonBehaviour);
			public uint address { get; private set; }

			public bool isSelf => true;

			private IVariable variable;

			public ThisVariableProxy(IVariable variable)
			{
				this.variable = variable;
			}

			public void Allocate(int count = 1) { }

			public void Use() { }

			public void SetAddress(uint address)
			{
				this.address = address;
				variable.SetAddress(address);
			}
		}
	}
}