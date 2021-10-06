using System;
using System.Collections.Generic;
using System.Reflection;
using Katsudon.Builder.Helpers;

namespace Katsudon.Builder.Extensions.Struct
{
	public class AsmStructInfo
	{
		public readonly Type type;
		public readonly Guid guid;
		public readonly bool isSerializable;

		public IReadOnlyList<FieldInfo> fields => _fields;

		private FieldInfo[] _fields = null;
		private Dictionary<FieldIdentifier, int> id2Index = new Dictionary<FieldIdentifier, int>();

		public AsmStructInfo(Type type, Guid guid, bool isSerializable)
		{
			this.type = type;
			this.guid = guid;
			this.isSerializable = isSerializable;
		}

		public void SetFields(FieldInfo[] fields)
		{
			_fields = fields;
			for(int i = 0; i < fields.Length; i++)
			{
				if(fields[i] != null)
				{
					id2Index[UdonCacheHelper.cache.GetFieldIdentifier(fields[i])] = i;
				}
			}
		}

		public int GetFieldIndex(FieldInfo field)
		{
			return id2Index[UdonCacheHelper.cache.GetFieldIdentifier(field)];
		}

		public int GetFieldIndex(FieldIdentifier field)
		{
			return id2Index[field];
		}
	}
}