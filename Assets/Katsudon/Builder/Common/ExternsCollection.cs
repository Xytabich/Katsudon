using System.Collections.Generic;

namespace Katsudon.Builder
{
	public class ExternsCollection
	{
		private Dictionary<string, IVariable> constants = new Dictionary<string, IVariable>();

		public IVariable GetVariable(string value)
		{
			IVariable variable;
			if(constants.TryGetValue(value, out variable))
			{
				return variable;
			}
			variable = new UnnamedSignificantVariable("extern", typeof(string), value);
			constants[value] = variable;
			return variable;
		}

		public void Apply(PropertiesBlock properties)
		{
			foreach(var item in constants)
			{
				properties.AddVariable(item.Value);
			}
		}
	}
}