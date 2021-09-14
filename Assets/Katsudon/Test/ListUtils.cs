using System;

public static class ListUtils
{
	public static void Ctor<T>(ref T[] array, ref int count, int capacity)
	{
		array = new T[capacity];
		count = 0;
	}

	public static void Add<T>(ref T[] array, ref int count, T value)
	{
		if(count >= array.Length)
		{
			var tmp = new T[count * 2];
			array.CopyTo(tmp, 0);
			array = tmp;
		}
		array[count] = value;
		count++;
	}

	public static bool Remove<T>(ref T[] array, ref int count, T value)
	{
		int index = Array.IndexOf((Array)array, value, 0, count);
		if(index < 0) return false;
		count--;
		Array.Copy(array, index + 1, array, index, count - index);
		array[count] = default;
		return true;
	}

	public static void RemoveAt<T>(ref T[] array, ref int count, int index)
	{
		count--;
		Array.Copy(array, index + 1, array, index, count - index);
		array[count] = default;
	}

	public static int IndexOf<T>(ref T[] array, ref int count, T value)
	{
		return Array.IndexOf((Array)array, value, 0, count);
	}

	public static int BinarySearch<T>(ref T[] array, ref int count, T value)
	{
		return Array.BinarySearch((Array)array, 0, count, value);
	}
}