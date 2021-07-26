using System;
using Katsudon.Editor.Udon;
using UnityEngine;
using VRC.Udon;

namespace Katsudon.Editor.Converters
{
	public class BehaviourTypes : IValueConverter
	{
		int IValueConverter.order => 50;

		bool IValueConverter.TryConvertToUdon(object value, out object converted)
		{
			if(value is MonoBehaviour proxy && Utils.IsUdonAsm(value.GetType()))
			{
				var behaviour = ProxyUtils.GetBehaviourByProxy(proxy);
				converted = behaviour;
				return true;
			}
			converted = null;
			return false;
		}

		bool IValueConverter.TryConvertFromUdon(object value, Type toType, out object converted)
		{
			if(value is UdonBehaviour ubeh && (toType.IsInterface || typeof(MonoBehaviour).IsAssignableFrom(toType)) && Utils.IsUdonAsm(toType))
			{
				var proxy = ProxyUtils.GetProxyByBehaviour(ubeh);
				if(proxy == null || toType.IsAssignableFrom(proxy.GetType()))
				{
					converted = proxy;
					return true;
				}
			}
			converted = null;
			return false;
		}
	}
}