using UnityEngine;

public class ColorablePacker : AbstractPacker, ICustomParameter<Color>
{
	[SerializeField]
	private Sprite _icon = null;
	[SerializeField]
	private GameObject packagePrefab = null;
	[SerializeField]
	private Transform packerRoot = null;

	public override string packerName => gameObject.name;

	public override Sprite icon => _icon;

	string ICustomParameter<Color>.title => "Ribbon color";

	private Color ribbonColor;

	public override Package Pack(GameObject item)
	{
		var obj = Instantiate(packagePrefab, packerRoot);
		var tr = obj.transform;
		tr.localPosition = Vector3.zero;
		tr.localRotation = Quaternion.identity;
		for(int i = tr.childCount - 1; i >= 0; i--)
		{
			var child = tr.GetChild(i);
			if(child.name == "Ribbon")
			{
				child.GetComponent<Renderer>().material.SetColor("_Color", ribbonColor);
				break;
			}
		}
		var package = obj.GetComponent<Package>();
		package.Init(item);
		return package;
	}

	void ICustomParameter<Color>.SetValue(Color color)
	{
		this.ribbonColor = color;
	}
}