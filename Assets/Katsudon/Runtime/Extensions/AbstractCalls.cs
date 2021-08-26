using UnityEngine;
using VRC.Udon.Common.Enums;
using VRC.Udon.Common.Interfaces;

public static class AbstractCallsHelper
{
	public static bool DisableInteractive(this MonoBehaviour behaviour)
	{
		return (behaviour as IUdonEventReceiver).DisableInteractive;
	}

	public static void DisableInteractive(this MonoBehaviour behaviour, bool value)
	{
		(behaviour as IUdonEventReceiver).DisableInteractive = value;
	}

	public static void RequestSerialization(this MonoBehaviour behaviour)
	{
		(behaviour as IUdonEventReceiver).RequestSerialization();
	}

	public static void SendCustomEvent(this MonoBehaviour behaviour, string eventName)
	{
		(behaviour as IUdonEventReceiver).SendCustomEvent(eventName);
	}

	public static void SendCustomEventDelayedFrames(this MonoBehaviour behaviour, string eventName, int delayFrames, EventTiming eventTiming = EventTiming.Update)
	{
		(behaviour as IUdonEventReceiver).SendCustomEventDelayedFrames(eventName, delayFrames, eventTiming);
	}

	public static void SendCustomEventDelayedSeconds(this MonoBehaviour behaviour, string eventName, float delaySeconds, EventTiming eventTiming = EventTiming.Update)
	{
		(behaviour as IUdonEventReceiver).SendCustomEventDelayedSeconds(eventName, delaySeconds, eventTiming);
	}

	public static void SendCustomNetworkEvent(this MonoBehaviour behaviour, NetworkEventTarget target, string eventName)
	{
		(behaviour as IUdonEventReceiver).SendCustomNetworkEvent(target, eventName);
	}
}