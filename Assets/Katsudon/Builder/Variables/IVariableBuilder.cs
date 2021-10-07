using System;

namespace Katsudon.Builder.Variables
{
	public interface IVariableBuilder
	{
		int order { get; }

		bool TryBuildVariable(IVariable variable, VariablesTable table);
	}
}