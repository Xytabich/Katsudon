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

		public IReadOnlyList<FieldInfo> fields => _fields;

		private List<FieldInfo> _fields = new List<FieldInfo>();
		private Dictionary<FieldIdentifier, int> id2Index = new Dictionary<FieldIdentifier, int>();

		public AsmStructInfo(Type type, Guid guid)
		{
			this.type = type;
			this.guid = guid;
		}

		public void AddField(FieldInfo field)
		{
			id2Index[UdonCacheHelper.cache.GetFieldIdentifier(field)] = _fields.Count;
			_fields.Add(field);
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