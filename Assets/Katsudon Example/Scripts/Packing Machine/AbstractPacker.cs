using UnityEngine;

public abstract class AbstractPacker : MonoBehaviour
{
	public abstract string packerName { get; }
	public abstract Sprite icon { get; }

	public abstract Package Pack(GameObject obj);
}