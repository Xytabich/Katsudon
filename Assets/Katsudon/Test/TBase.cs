using UnityEngine;

public abstract class TBase : MonoBehaviour
{
	protected float test = 10f;

	public virtual void Test(TestUdonBehaviour var)
	{
		Debug.Log(var.test < 11f);
		Debug.Log(var.test > 11f);
		Debug.Log("T:" + (var.test < 11f) + ":" + (var.test > 11f));
		var.test = 11f;
	}
}