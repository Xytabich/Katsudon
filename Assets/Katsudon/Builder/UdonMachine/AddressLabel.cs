using System;

namespace Katsudon.Builder
{
	public interface IAddressLabel
	{
		uint address { get; }
	}

	public interface IEmbedAddressLabel : IAddressLabel
	{
		void Init(Func<uint> pointerGetter);

		void Apply();
	}

	public class EmbedAddressLabel : IEmbedAddressLabel
	{
		uint IAddressLabel.address
		{
			get
			{
				if(!_address.HasValue) throw new Exception("Address is not assigned");
				return _address.Value;
			}
		}

		private uint? _address;
		private Func<uint> pointerGetter;

		void IEmbedAddressLabel.Init(Func<uint> pointerGetter)
		{
			this.pointerGetter = pointerGetter;
		}

		void IEmbedAddressLabel.Apply()
		{
			_address = pointerGetter();
		}
	}
}