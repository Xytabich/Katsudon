using System;

namespace Katsudon
{
	/// <summary>
	/// Used to determine the position of a field in a serializable type.
	/// </summary>
	[AttributeUsage(AttributeTargets.Field)]
	public class FieldPositionAttribute : Attribute
	{
		public readonly int position;

		/// <summary>
		/// Used to determine the position of a field in a serializable type.
		/// </summary>
		/// <param name="position">The ordinal number of the field (0-based).</param>
		public FieldPositionAttribute(int position)
		{
			this.position = position;
		}
	}
}