
using System;

namespace Katsudon.Builder
{
	public struct VariableMeta
	{
		public readonly IVariable variable;
		public readonly Type preferredType;
		public readonly UsageMode usageMode;

		public VariableMeta(IVariable variable, Type preferredType)
		{
			this.variable = variable;
			this.preferredType = preferredType;
			this.usageMode = UsageMode.None;
		}

		public VariableMeta(IVariable variable, Type preferredType, UsageMode usageMode)
		{
			this.variable = variable;
			this.preferredType = preferredType;
			this.usageMode = usageMode;
		}

		public enum UsageMode
		{
			None = 0,
			In = 1,
			Out = 2,
			OutOnly = 4
		}
	}

	public static class VariableMetaExtension
	{
		public static VariableMeta UseType(this IVariable variable, Type preferredType)
		{
			return new VariableMeta(variable, preferredType.IsByRef ? preferredType.GetElementType() : preferredType);
		}

		public static VariableMeta OwnType(this IVariable variable)
		{
			return UseType(variable, variable.type);
		}

		public static VariableMeta Mode(this IVariable variable, VariableMeta.UsageMode mode)
		{
			return Mode(OwnType(variable), mode);
		}

		public static VariableMeta Mode(this VariableMeta variable, VariableMeta.UsageMode mode)
		{
			return new VariableMeta(variable.variable, variable.preferredType, variable.usageMode | mode);
		}
	}
}