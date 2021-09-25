using System;
using UnityEngine;

public class PackingMachine : MonoBehaviour
{
	[SerializeField]
	private Bounds pickArea = default;
	[SerializeField]
	private LayerMask itemMask = default;
	[SerializeField]
	private Animator animator = null;
	[SerializeField]
	private Transform itemRoot = null;

	[NonSerialized]
	public bool isPacking = false;

	public AbstractPacker[] packers => _packers ?? (_packers = GetComponentsInChildren<AbstractPacker>());

	private AbstractPacker[] _packers;

	private AbstractPacker activePacker;

	private bool isPackStarted = false;
	private bool isPackEnded = false;
	private GameObject targetObj = null;
	private Package packedObj = null;

	public void Pack(int packerIndex)
	{
		if(isPacking) return;
		activePacker = packers[packerIndex];
		var collisions = Physics.OverlapBox(transform.position + pickArea.center, pickArea.extents, Quaternion.identity, itemMask);
		if(collisions.Length > 0)
		{
			targetObj = collisions[0].gameObject;
			var rb = targetObj.GetComponent<Rigidbody>();
			if(rb != null) rb.isKinematic = true;

			targetObj.transform.SetParent(itemRoot);

			isPacking = true;
			isPackEnded = false;
			isPackStarted = false;
			animator.Play("StartPack");
		}
	}

	void Update()
	{
		if(isPacking)
		{
			var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
			if(isPackEnded)
			{
				if(!stateInfo.IsName("EndPack"))
				{
					isPacking = false;
					packedObj.SetActive(true);
					packedObj.transform.SetParent(null);
					packedObj.onUnpacked += ReleaseObject;
					packedObj = null;
				}
			}
			else if(isPackStarted)
			{
				if(!stateInfo.IsName("StartPack"))
				{
					isPackEnded = true;
					targetObj.SetActive(false);
					targetObj.transform.SetParent(null);

					packedObj = activePacker.Pack(targetObj);
					targetObj = null;

					packedObj.SetActive(false);
				}
			}
			else
			{
				if(stateInfo.IsName("StartPack"))
				{
					isPackStarted = true;
				}
			}
		}
	}

	private void ReleaseObject(GameObject obj)
	{
		obj.SetActive(true);
		var rb = obj.GetComponent<Rigidbody>();
		if(rb != null) rb.isKinematic = false;
	}

	void OnDrawGizmos()
	{
		Gizmos.DrawWireCube(transform.position + pickArea.center, pickArea.size);
	}
}