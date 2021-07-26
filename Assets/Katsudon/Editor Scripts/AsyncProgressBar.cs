using System;

namespace Katsudon.Editor
{
	public static class AsyncProgressBar
	{
		private static Action clearBar = (Action)Delegate.CreateDelegate(typeof(Action), typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AsyncProgressBar").GetMethod("Clear"));
		private static Action<string, float> displayBar = (Action<string, float>)Delegate.CreateDelegate(typeof(Action<string, float>), typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AsyncProgressBar").GetMethod("Display"));

		public static void Display(string progressInfo, float progress)
		{
			displayBar(progressInfo, progress);
		}

		public static void Clear()
		{
			clearBar();
		}
	}
}