using System.Runtime.CompilerServices;
using Katsudon;
using UnityEngine;

[assembly: UdonAsm]
public abstract class WTest<T> : MonoBehaviour, ITest<WTest<T>>
{
	private const int ZERO = 0;

	public string _p;

	public int a { get { return 0; } }
	public int b { set { } }
	public abstract int c { get; set; }
	public int d { get; private set; }
	public string gp => _p;
	public string sp { set { _p = value; } }
	protected int e { get; private set; }

	protected abstract void Start();

	public void Update()
	{
		if(c == 10) return;
		if(d == 10) return;
		this.c = this.d;
		_p = _p + "4";
	}

	/*public virtual string Abc(int a, string b, bool c)
	{
		b += a;
		b += c;
		return b;
	}*/

	public virtual int Abc(WTest<T> other)
	{
		var anc = other._p;

		return Tst(other.a);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int Tst(int s)
	{
		return s + 1;
	}

	public void WT(WTest<T> wt)
	{

	}

	public abstract void TM(T value);

	protected void WTT(WTest<T> other)
	{
		other.WT(this);
	}
}

public interface ITest<T>
{
	int Abc(T other);
}