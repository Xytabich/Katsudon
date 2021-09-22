using System;

namespace Katsudon
{
	[AttributeUsage(AttributeTargets.Field)]
	public sealed class SyncAttribute : Attribute
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
		None,
		Linear,
		Smooth,
	}
}