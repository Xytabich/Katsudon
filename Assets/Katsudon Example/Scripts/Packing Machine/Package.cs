using System;
using UnityEngine;
using VRC.SDK3.Components;

public class Package : MonoBehaviour
{
	[SerializeField]
	private VRCPickup pickup = null;
	[SerializeField]
	private AudioSource sound = null;

	public event Action<GameObject> onUnpacked;

	private GameObject item;
	private DateTime startUnpackTime;

	public void Init(GameObject item)
	{
		this.item = item;
	}

	public void SetActive(bool isActive)
	{
		pickup.enabled = isActive;
		var rb = GetComponent<Rigidbody>();
		if(rb != null) rb.isKinematic = !isActive;
	}

	void OnPickupUseDown()
	{
		startUnpackTime = DateTime.Now;
		sound.Play();
	}

	void OnPickupUseUp()
	{
		sound.Stop();
		if((DateTime.Now - startUnpackTime).TotalSeconds >= 2f)
		{
			OnUnpacked();
		}
	}

	private void OnUnpacked()
	{
		var tr = item.transform;
		tr.position = transform.position;
		tr.rotation = transform.rotation;
		onUnpacked.Invoke(item);
		pickup.Drop();
		Destroy(gameObject);
	}
}