using UnityEngine;

public class ListTests : MonoBehaviour
{
	void Start()
	{
		string[] listArray;
		int listCount;
		ListUtils.Ctor(out listArray, out listCount, 10);
		for(int i = 0; i < 1000; i++)
		{
			ListUtils.Add(ref listArray, ref listCount, (1000 - i).ToString());
		}
		Debug.Log("Index: " + ListUtils.IndexOf(listArray, listCount, "100"));
		Debug.Log("All elements: " + string.Join(", ", listArray, 0, listCount));
		ListUtils.Remove(listArray, ref listCount, "100");
		Debug.Log("Removed Index: " + ListUtils.IndexOf(listArray, listCount, "100"));
	}
}