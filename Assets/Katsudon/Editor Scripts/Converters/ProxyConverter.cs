using System;
using System.Collections.Generic;
using Katsudon.Editor.Udon;
using UnityEngine;
using VRC.Udon;

namespace Katsudon.Editor.Converters
{
	[ValueConverter]
	public class ProxyConverter : IValueConverter
	{
		int IValueConverter.order => 50;

		bool IValueConverter.TryConvertToUdon(object value, out object converted, out bool isAllowed)
		{
			if(value is MonoBehaviour proxy && Utils.IsUdonAsm(value.GetType()))
			{
				var behaviour = ProxyUtils.GetBehaviourByProxy(proxy);
				converted = behaviour;
				isAllowed = true;
				return true;
			}
			converted = null;
			isAllowed = false;
			return false;
		}

		bool IValueConverter.TryConvertFromUdon(object value, Type toType, out object converted, out bool isAllowed, ref bool reserialize)
		{
			if(value is UdonBehaviour ubeh && Utils.IsUdonAsmBehaviourOrInterface(toType))
			{
				var proxy = ProxyUtils.GetProxyByBehaviour(ubeh);
				if(proxy == null || toType.IsAssignableFrom(proxy.GetType()))
				{
					converted = proxy;
					isAllowed = true;
					return true;
				}
			}
			converted = null;
			isAllowed = false;
			return false;
		}

		public static void Register(UdonValueResolver resolver, ICollection<IValueConverter> container)
		{
			container.Add(new ProxyConverter());
		}
	}
}