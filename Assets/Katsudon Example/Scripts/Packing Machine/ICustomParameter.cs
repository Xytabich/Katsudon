using UnityEngine;

public interface ICustomParameter<T>
{
	string title { get; }

	void SetValue(T value);
}