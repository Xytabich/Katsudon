using System;
using System.Collections.Generic;

namespace Katsudon.Builder.Helpers
{
	public struct MagicMethodInfo
	{
		public string udonName;
		public Type returnType;
		public KeyValuePair<string, Type>[] parameters;

		public MagicMethodInfo(string udonName, Type returnType, KeyValuePair<string, Type>[] parameters)
		{
			this.udonName = udonName;
			this.returnType = returnType;
			this.parameters = parameters;
		}
	}
}