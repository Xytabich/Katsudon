using System;

namespace Katsudon
{
	/// <summary>
	/// A label indicating that the assembly needs to be assembled into a udon assembly.
	/// </summary>
	[AttributeUsage(AttributeTargets.Assembly)]
	public class UdonAsmAttribute : Attribute { }
}