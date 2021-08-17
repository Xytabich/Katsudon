using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;
using VRC.Udon;

public class TUBeh : TBase, TInt
{
	//public TUBeh other;

	//public float tg => test;
	//public float tp { get => test; set => test = value; }

	/*[method: MethodImpl(MethodImplOptions.Synchronized)]
	public event System.Action evt;*/

	[SerializeField]
	private Image img = null;
	public Sprite sprite;
	public TuTuBeh other;

	public int counter = 0;

	private TimeSpan dt = DateTime.Now - new DateTime(1999, 9, 16).AddMonths(129);
	private Image.FillMethod fm = Image.FillMethod.Radial180;
	private Vector3 v = new Vector3(-1, -2, 16);
	private int[] b = new int[] { 1050 };
	private uint @extern = (uint)(!DateTime.Now.IsDaylightSavingTime() ? 0x16 : 0x18);

	private bool abc = true;
	private bool ibc = false;

	private Type t = typeof(int);

	private event Func<int, string, bool> evt;
	private TUBeh self;
	private byte bt;

	public void Start()
	{
		/*var abc = Image.Type.Filled;
		var bcd = Image.Type.Sliced;
		Debug.Log(abc | bcd);*/

		// 	var tint = this as TInt;

		// 	img.fillMethod = Image.FillMethod.Horizontal;
		// 	goto _label;
		// _label2:
		// 	img.fillMethod = Image.FillMethod.Vertical;
		// 	goto _outLabel;
		// _label:
		// 	img.fillMethod = Image.FillMethod.Radial360;
		// 	if(img.sprite != null) goto _label2;
		// 	_outLabel:
		// 	tint.TestA(img.name);
		// 	Test(this);

		// 		Debug.Log((abc & ibc) + ":" + (abc | ibc));
		// Debug.Log(GetComponentInChildren<Image>(false));
		// Debug.Log("C:" + GetComponents<Image>().Length);
		// Debug.Log("C:" + GetComponentsInChildren(typeof(TuTuBeh), true).Length);
		// Debug.Log("C:" + GetComponentsInParent<TuTuBeh>(true).Length);
		// Debug.Log(GetComponentInParent<TuTuBeh>());
		// Debug.Log(GetComponentInParent<Image>());

		/*uint[,,] abc = new uint[1, 2, 3];
		Debug.Log(abc.Length);*/

		/*var bcd = new int[1, 2, 3];
		bcd[0, 1, 2] = bcd[0, 0, 0];
		Debug.Log(bcd.Length);*/

		/*UnityEngine.Debug.Log(test);
		other.Test(this);
		UnityEngine.Debug.Log(test);*/

		bt = 1;
		int cb = ~(int)bt;
		unchecked
		{
			Debug.Log((byte)cb + ":" + (~(byte)1));
		}

		foreach(var p in b)
		{
			Debug.Log(p);
		}

		self = this;

		Debug.Log(TstEvt(0, "0"));
		Debug.Log(TSCall(3, 8));

		self.evt = TstEvt;
		self.evt += TstEvt;
		self.evt += TstEvt;
		Debug.Log(self.evt.Invoke(0, "0"));
		self.evt -= TstEvt;
		self.evt -= null;
		self.evt += null;
		Debug.Log(self.evt);
		evt += evt;
		Debug.Log(evt.Invoke(1, "0"));
		evt -= evt;
		Debug.Log(evt);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool TstEvt(int a, string b)
	{
		Debug.Log(counter++);
		return a.ToString() == b;
	}

	void Update()
	{
		counter++;
	}

	public override void Test(TUBeh var)
	{
		var.test = 111f;
		base.Test(var);
		ulong abc = '0';
		uint bcd = (uint)abc;
		Debug.Log(bcd);
	}

	public int Abb() { return 0; }

	void OnDrawGizmos()
	{
		Gizmos.DrawCube(transform.position + Vector3.up * (counter / 1000f), Vector3.one);
	}

	/*public void TestEditor()
	{
		bool a = true;
		bool b = false;
		if(a && b && BF())
		{
			RectOffset[,] t = new RectOffset[1, 1];
			t[0, 0].left = 10;
		}
		checked
		{
			int abc = 0;
			uint cbc = (uint)abc;
		}
	}*/

	//private bool BF() { return false; }

	/*public void V() { }

	//public Action Get() { return V; }

	public float GetTest()
	{
		int abc = 0;
		switch(abc)
		{
			case 0:
				Debug.Log(0);
				break;
			case 1:
				Debug.Log(0);
				break;
			case 10:
				Debug.Log(10);
				break;
			default:
				Debug.Log(-1);
				break;
		}

		return test;
	}

	public float GetTested(TUBeh test)
	{
		return this.test = test.test;
	}

	public void GGTT(TUBeh test)
	{
		test.GGTT(this);
	}

	public void SetTest(float v)
	{
		//evt.Invoke();
		test = v;
	}*/

	public void TestA(string a)
	{
		Debug.Log(a);
	}

	int TInt.TestB(int abs)
	{
		return abs;
	}

	private static int TSCall(int a, int b)
	{
		int c = b * a;
		return a + c;
	}
}

public interface TInt
{
	void TestA(string a);
	int TestB(int abs);
}