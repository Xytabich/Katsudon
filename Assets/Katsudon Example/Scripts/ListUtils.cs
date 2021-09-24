using System;
using System.ComponentModel;

public static class ListUtils
{
	public static void Ctor<T>(out T[] array, out int count, [ReadOnly(true)] int capacity)
	{
		array = new T[capacity];
		count = 0;
	}

	public static void Add<T>(ref T[] array, ref int count, [ReadOnly(true)] T value)
	{
		EnsureCapacity(ref array, count);
		array[count] = value;
		count++;
	}

	public static void Insert<T>(ref T[] array, ref int count, [ReadOnly(true)] int index, [ReadOnly(true)] T value)
	{
		EnsureCapacity(ref array, count);
		Array.Copy(array, index, array, index + 1, count - index);
		array[index] = value;
		count++;
	}

	public static bool Remove<T>(in T[] array, ref int count, [ReadOnly(true)] T value)
	{
		int index = Array.IndexOf((Array)array, value, 0, count);
		if(index < 0) return false;
		count--;
		Array.Copy(array, index + 1, array, index, count - index);
		array[count] = default;
		return true;
	}

	public static void RemoveAt<T>(in T[] array, ref int count, [ReadOnly(true)] int index)
	{
		count--;
		Array.Copy(array, index + 1, array, index, count - index);
		array[count] = default;
	}

	public static int IndexOf<T>(in T[] array, in int count, [ReadOnly(true)] T value)
	{
		return Array.IndexOf((Array)array, value, 0, count);
	}

	public static int LastIndexOf<T>(in T[] array, in int count, [ReadOnly(true)] T value)
	{
		return Array.LastIndexOf((Array)array, value, 0, count);
	}

	public static int BinarySearch<T>(in T[] array, in int count, [ReadOnly(true)] T value)
	{
		return Array.BinarySearch((Array)array, 0, count, value);
	}

	public static void Sort<T>(in T[] array, in int count)
	{
		Array.Sort((Array)array, 0, count);
	}

	public static T[] ToArray<T>(in T[] array, in int count)
	{
		var result = new T[count];
		Array.Copy(array, result, count);
		return result;
	}

	private static void EnsureCapacity<T>(ref T[] array, in int count)
	{
		if(count >= array.Length)
		{
			var tmp = new T[count * 2];
			array.CopyTo(tmp, 0);
			array = tmp;
		}
	}
}