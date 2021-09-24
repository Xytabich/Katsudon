using System.ComponentModel;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

public class PackingMachineUI : MonoBehaviour
{
	[SerializeField]
	private PackingMachine machine = null;
	[SerializeField]
	private Selectable indexVariable = null;
	[SerializeField]
	private RectTransform iconsRoot = null;
	[SerializeField]
	private Image iconPrefab = null;

	[SerializeField]
	private Image packerIcon = null;
	[SerializeField]
	private Text packerName = null;
	[SerializeField]
	private Text colorSliderTitle = null;
	[SerializeField]
	private Slider colorSlider = null;
	[SerializeField]
	private Image colorSliderHandle = null;

	private int packerIndex;
	private AbstractPacker packer;

	void Start()
	{
		var packers = machine.packers;
		for(int i = 0; i < packers.Length; i++)
		{
			var icon = Instantiate(iconPrefab, iconsRoot);
			icon.sprite = packers[i].icon;
			var obj = icon.gameObject;
			obj.name = i.ToString();
			obj.SetActive(true);
		}
		SelectPacker(0);
		UpdateColor();
	}

	public void OnSelectPacker()
	{
		SelectPacker(int.Parse(indexVariable.targetGraphic.gameObject.name));
	}

	public void StartPacking()
	{
		if(machine.isPacking) return;
		if(packer is ICustomParameter<Color> param)
		{
			param.SetValue(Color.HSVToRGB(colorSlider.value, 1f, 1f));
		}
		machine.Pack(packerIndex);
	}

	public void UpdateColor()
	{
		colorSliderHandle.color = Color.HSVToRGB(colorSlider.value, 1f, 1f);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void SelectPacker([ReadOnly(true)] int index)
	{
		this.packerIndex = index;
		packer = machine.packers[index];
		packerIcon.sprite = packer.icon;
		packerName.text = packer.packerName;
		if(packer is ICustomParameter<Color> param)
		{
			colorSlider.gameObject.SetActive(true);
			colorSliderTitle.text = param.title;
		}
		else
		{
			colorSlider.gameObject.SetActive(false);
		}
	}
}