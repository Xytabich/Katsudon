using System;
using System.Collections.Generic;

namespace Katsudon.Builder.Helpers
{
	public struct GenericTypeIndexer
	{
		private Dictionary<Type, int> type2Index;
		private int typeCounter;

		public GenericTypeIndexer(Dictionary<Type, int> cached)
		{
			this.type2Index = cached;
			this.typeCounter = 0;
		}

		public int GetTypeIdentifier(Type type)
		{
			if(!type2Index.TryGetValue(type, out var index))
			{
				typeCounter--;
				index = typeCounter;
				type2Index[type] = index;
			}
			return index;
		}
	}
}