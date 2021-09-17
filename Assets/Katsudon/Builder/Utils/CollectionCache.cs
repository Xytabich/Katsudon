using System.Collections.Concurrent;
using System.Collections.Generic;

public static class CollectionCache
{
	public static List<T> GetList<T>()
	{
		return InnerCache<List<T>, T>.GetCollection();
	}

	public static List<T> GetList<T>(IReadOnlyCollection<T> copyFrom)
	{
		var collection = InnerCache<List<T>, T>.GetCollection();
		if(collection.Capacity < copyFrom.Count) collection.Capacity = copyFrom.Count;
		collection.AddRange(copyFrom);
		return collection;
	}

	public static Stack<T> GetStack<T>()
	{
		return InnerCache<Stack<T>, T>.GetCollection();
	}

	public static HashSet<T> GetSet<T>()
	{
		return InnerCache<HashSet<T>, T>.GetCollection();
	}

	public static Dictionary<K, V> GetDictionary<K, V>()
	{
		return InnerCache<Dictionary<K, V>, KeyValuePair<K, V>>.GetCollection();
	}

	public static void Release<T>(List<T> collection)
	{
		collection.Clear();
		InnerCache<List<T>, T>.ReleaseCollection(collection);
	}

	public static void Release<T>(Stack<T> collection)
	{
		collection.Clear();
		InnerCache<Stack<T>, T>.ReleaseCollection(collection);
	}

	public static void Release<T>(HashSet<T> collection)
	{
		collection.Clear();
		InnerCache<HashSet<T>, T>.ReleaseCollection(collection);
	}

	public static void Release<K, V>(Dictionary<K, V> collection)
	{
		collection.Clear();
		InnerCache<Dictionary<K, V>, KeyValuePair<K, V>>.ReleaseCollection(collection);
	}

	private static class InnerCache<T, V> where T : IReadOnlyCollection<V>, new()
	{
		private static ConcurrentStack<T> stack = new ConcurrentStack<T>();

		public static T GetCollection()
		{
			if(stack.TryPop(out var collection))
			{
				if(collection.Count > 0) throw new System.Exception("Collection is used after release");
				return collection;
			}
			return new T();
		}

		public static void ReleaseCollection(T collection)
		{
			stack.Push(collection);
		}
	}
}