using System;
using System.Reflection;

namespace Katsudon.Builder.Helpers
{
	public struct MethodIdentifier : IEquatable<MethodIdentifier>
	{
		internal string name;
		internal int declaringType;
		internal int[] arguments;

		private int hashCode;

		public MethodIdentifier(IUdonPartsCache cache, MethodBase info) : this(cache, info.DeclaringType, info) { }

		public MethodIdentifier(IUdonPartsCache cache, Type calleeType, MethodBase info)
		{
			name = info.Name;
			declaringType = cache.GetTypeIdentifier(calleeType);

			var parameters = info.GetParameters();
			arguments = new int[parameters.Length];
			if(info.IsGenericMethodDefinition)
			{
				var genericIndexer = cache.GetGenericTypeIndexer();
				for(var i = 0; i < parameters.Length; i++)
				{
					var type = parameters[i].ParameterType;
					if(type.IsByRef) type = type.GetElementType();
					arguments[i] = type.ContainsGenericParameters ? genericIndexer.GetTypeIdentifier(type) : cache.GetTypeIdentifier(type);
				}
			}
			else
			{
				for(var i = 0; i < parameters.Length; i++)
				{
					var type = parameters[i].ParameterType;
					if(type.IsByRef) type = type.GetElementType();
					arguments[i] = cache.GetTypeIdentifier(type);
				}
			}

			this.hashCode = CalcHash(name, declaringType, arguments);
		}

		public MethodIdentifier(int declaringType, string name, int[] arguments)
		{
			this.declaringType = declaringType;
			this.arguments = arguments;
			this.name = name;

			this.hashCode = CalcHash(name, declaringType, arguments);
		}

		public override bool Equals(object obj)
		{
			return obj is MethodIdentifier identifier && Equals(identifier);
		}

		public bool Equals(MethodIdentifier other)
		{
			if(declaringType == other.declaringType && name == other.name)
			{
				var args = other.arguments;
				int len = arguments.Length;
				if(len == args.Length)
				{
					if(len > 0)
					{
						unsafe
						{
							fixed(int* a = arguments, b = args)
							{
								for(var i = 0; i < len; i++)
								{
									if(a[i] != b[i]) return false;
								}
							}
						}
					}
					return true;
				}
			}
			return false;
		}

		public override int GetHashCode()
		{
			return hashCode;
		}

		private static int CalcHash(string name, int declaringType, int[] arguments)
		{
			int hashCode = -890188069;
			hashCode = hashCode * -1521134295 + name.GetHashCode();
			hashCode = hashCode * -1521134295 + declaringType;
			for(var i = 0; i < arguments.Length; i++)
			{
				hashCode = hashCode * -1521134295 + arguments[i];
			}
			return hashCode;
		}

		public static bool operator ==(MethodIdentifier a, MethodIdentifier b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(MethodIdentifier a, MethodIdentifier b)
		{
			return !a.Equals(b);
		}
	}
}