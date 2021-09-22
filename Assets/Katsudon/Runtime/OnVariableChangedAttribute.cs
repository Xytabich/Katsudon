using System;

namespace Katsudon
{
	/// <summary>
	/// The method marked with this attribute will listen for a field change event with the specified name.
	/// Method format: void MethodName(FIELD_TYPE oldValue);
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	public sealed class OnVariableChangedAttribute : Attribute
	{
		public readonly string fieldName;

		public OnVariableChangedAttribute(string fieldName)
		{
			this.fieldName = fieldName;
		}
	}
}