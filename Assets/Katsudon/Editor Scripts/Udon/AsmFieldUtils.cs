using Katsudon.Builder.Converters;
using Katsudon.Info;
using UnityEngine;

namespace Katsudon.Editor.Udon
{
	public static class AsmFieldUtils
	{
		public delegate bool TryGetBehaviourVariable(string name, out object value);
		public delegate void TrySetBehaviourVariable(string name, object value);

		public static bool TryCopyValueToProxy(AsmFieldInfo field, MonoBehaviour proxy, TryGetBehaviourVariable variableGetter, out bool reserialize)
		{
			if(variableGetter(field.name, out var value))
			{
				if(UdonValueResolver.instance.TryConvertFromUdon(value, field.field.FieldType, out var converted, out reserialize))
				{
					field.field.SetValue(proxy, converted);
					return true;
				}
			}
			reserialize = false;
			return false;
		}

		public static bool TryCopyValueToBehaviour(AsmFieldInfo field, MonoBehaviour proxy, TrySetBehaviourVariable variableSetter)
		{
			if(UdonValueResolver.instance.TryConvertToUdon(field.field.GetValue(proxy), out var converted))
			{
				variableSetter(field.name, converted);
				return true;
			}
			return false;
		}
	}
}