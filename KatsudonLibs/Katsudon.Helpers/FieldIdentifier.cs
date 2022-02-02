using System;
using System.Reflection;

namespace Katsudon.Builder.Helpers
{
	public struct FieldIdentifier : IEquatable<FieldIdentifier>
	{
		internal int declaringType;
		internal string name;

		private int hashCode;

		public FieldIdentifier(IUdonPartsCache cache, FieldInfo info) : this(cache.GetTypeIdentifier(info.DeclaringType), info.Name) { }

		public FieldIdentifier(int declaringType, string name)
		{
			this.declaringType = declaringType;
			this.name = name;

			hashCode = -117268428;
			hashCode = hashCode * -1521134295 + declaringType.GetHashCode();
			hashCode = hashCode * -1521134295 + name.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			return obj is FieldIdentifier identifier && Equals(identifier);
		}

		public bool Equals(FieldIdentifier other)
		{
			return declaringType == other.declaringType && name == other.name;
		}

		public override int GetHashCode()
		{
			return hashCode;
		}

		public static bool operator ==(FieldIdentifier a, FieldIdentifier b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(FieldIdentifier a, FieldIdentifier b)
		{
			return !a.Equals(b);
		}
	}
}