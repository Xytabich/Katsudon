using System;

namespace Katsudon
{
	[AttributeUsage(AttributeTargets.Field)]
	public class SyncAttribute : Attribute
	{
		public readonly SyncMode mode;

		public SyncAttribute(SyncMode mode = SyncMode.None)
		{
			this.mode = mode;
		}
	}

	public enum SyncMode
	{
		NotSynced,
		None, // No interpolation
		Linear, // Lerp
		Smooth, // Some kind of smoothed syncing, no idea what curve they apply to it
	}
}