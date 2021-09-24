using UnityEngine;

public class SimplePacker : AbstractPacker
{
	[SerializeField]
	private Sprite _icon = null;
	[SerializeField]
	private GameObject packagePrefab = null;
	[SerializeField]
	private Transform packerRoot = null;

	public override string packerName => gameObject.name;

	public override Sprite icon => _icon;

	public override Package Pack(GameObject item)
	{
		var obj = Instantiate(packagePrefab, packerRoot);
		var tr = obj.transform;
		tr.localPosition = Vector3.zero;
		tr.localRotation = Quaternion.identity;
		var package = obj.GetComponent<Package>();
		package.Init(item);
		return package;
	}
}