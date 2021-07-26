using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestInstantiate : MonoBehaviour
{
	public Transform prefab;

	void Start()
	{
		Debug.Log(transform.TransformVector(prefab.transform.lossyScale));
		Instantiate(prefab).name = "Empty";//Копируются ЛОКАЛЬНЫЕ координаты(Instaniate>instance.localPosition=prefab.localPosition)
		Instantiate(prefab, transform).name = "Parent";//То же что и Instantiate(prefab, transform, false)
		Instantiate(prefab, transform, true).name = "Parent WPS";//Копируются ГЛОБАЛЬНЫЕ координаты(Instaniate>instance.position=prefab.position)
		Instantiate(prefab, transform, false).name = "Parent NWPS";//Копируются ЛОКАЛЬНЫЕ координаты(Instaniate>instance.localPosition=prefab.localPosition)
		Instantiate(prefab, Vector3.one, Quaternion.Euler(30f, 0f, 0f)).name = "Position";//Устанавливаются ГЛОБАЛЬНЫЕ координаты(Instaniate>instance.position=pos)
		Instantiate(prefab, Vector3.one, Quaternion.Euler(30f, 0f, 0f), transform).name = "Parent Position";//Устанавливаются ГЛОБАЛЬНЫЕ координаты(Instaniate>instance.position=pos)

		var tst = Instantiate(prefab.gameObject);
		tst.name = "TST true";
		tst.transform.SetParent(transform, true);
		Debug.Log(tst.GetComponents(typeof(Component))[Array.IndexOf(prefab.GetComponents(typeof(Component)), prefab)]);
	}
}