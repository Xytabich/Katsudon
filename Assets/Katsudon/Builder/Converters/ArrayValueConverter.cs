using System;
using System.Collections.Generic;

namespace Katsudon.Builder.Converters
{
	[ValueConverter]
	public class ArrayValueConverter : IValueConverter
	{
		int IValueConverter.order => 100;

		private UdonValueResolver resolver;

		public ArrayValueConverter(UdonValueResolver resolver)
		{
			this.resolver = resolver;
		}

		bool IValueConverter.TryConvertToUdon(object value, out object converted, out bool isAllowed)
		{
			var type = value.GetType();
			if(type.IsArray && !Utils.IsUdonType(type))
			{
				var rank = type.GetArrayRank();
				var inArray = (Array)value;
				if(rank <= 1)
				{
					var arrayType = GetOneDimensionalType(type);
					if(arrayType != null)
					{
						var outArray = Array.CreateInstance(arrayType.GetElementType(), inArray.LongLength);
						for(long i = inArray.LongLength - 1; i >= 0; i--)
						{
							if(resolver.TryConvertToUdon(inArray.GetValue(i), out var v))
							{
								outArray.SetValue(v, i);
							}
						}
						converted = outArray;
						isAllowed = true;
						return true;
					}
				}
				else
				{
					var elementType = GetElementType(type.GetElementType());
					if(Utils.IsUdonType(elementType))
					{
						converted = value;
						isAllowed = true;
						return true;
					}

					int[] lengths = new int[rank];
					int[] indices = new int[rank];
					for(int i = 0; i < rank; i++)
					{
						lengths[i] = inArray.GetLength(i);
						indices[i] = 0;
					}
					var outArray = Array.CreateInstance(typeof(object), lengths);
					bool fill = true;
					while(fill)
					{
						if(resolver.TryConvertToUdon(inArray.GetValue(indices), out var v))
						{
							outArray.SetValue(v, indices);
						}
						indices[0]++;
						int i = 0;
						while(indices[i] >= lengths[i])
						{
							indices[i] = 0;
							i++;
							if(i >= rank)
							{
								fill = false;
								break;
							}
							indices[i]++;
						}
					}
					converted = outArray;
					isAllowed = true;
					return true;
				}
			}
			converted = null;
			isAllowed = false;
			return false;
		}

		bool IValueConverter.TryConvertFromUdon(object value, Type toType, out object converted, out bool isAllowed, ref bool reserialize)
		{
			if(toType.IsArray && !Utils.IsUdonType(toType))
			{
				if(value.GetType() == toType)
				{
					converted = value;
					isAllowed = true;
					return true;
				}

				var elementType = toType.GetElementType();
				var rank = toType.GetArrayRank();
				var inArray = (Array)value;
				if(rank <= 1)
				{
					var outArray = Array.CreateInstance(elementType, inArray.LongLength);
					for(long i = inArray.LongLength - 1; i >= 0; i--)
					{
						if(resolver.TryConvertFromUdon(inArray.GetValue(i), elementType, out var v, out bool r))
						{
							outArray.SetValue(v, i);
							if(r) reserialize = true;
						}
						else reserialize = true;
					}
					converted = outArray;
					isAllowed = true;
					return true;
				}
				else
				{
					int[] lengths = new int[rank];
					int[] indices = new int[rank];
					for(int i = 0; i < rank; i++)
					{
						lengths[i] = inArray.GetLength(i);
						indices[i] = 0;
					}
					var outArray = Array.CreateInstance(elementType, lengths);
					bool fill = true;
					while(fill)
					{
						if(resolver.TryConvertFromUdon(inArray.GetValue(indices), elementType, out var v, out bool r))
						{
							outArray.SetValue(v, indices);
							if(r) reserialize = true;
						}
						else reserialize = true;
						indices[0]++;
						int i = 0;
						while(indices[i] >= lengths[i])
						{
							indices[i] = 0;
							i++;
							if(i >= rank)
							{
								fill = false;
								break;
							}
							indices[i]++;
						}
					}
					converted = outArray;
					isAllowed = true;
					return true;
				}
			}
			converted = null;
			isAllowed = false;
			return false;
		}

		Type IValueConverter.GetUdonType(Type type)
		{
			if(type.IsArray && !Utils.IsUdonType(type))
			{
				var rank = type.GetArrayRank();
				if(rank <= 1)
				{
					return GetOneDimensionalType(type);
				}
				else
				{
					return typeof(Array);
				}
			}
			return null;
		}

		private Type GetOneDimensionalType(Type type)
		{
			if(resolver.TryGetUdonType(type.GetElementType(), out var elementType))
			{
				Type arrayType;

				arrayType = elementType.MakeArrayType();
				while(!Utils.IsUdonType(arrayType))
				{
					elementType = elementType.BaseType;
					arrayType = elementType.MakeArrayType();
				}

				return arrayType;
			}
			return null;
		}

		private static Type GetElementType(Type elementType)
		{
			while(elementType.IsArray)
			{
				elementType = elementType.GetElementType();
			}
			return elementType;
		}

		public static void Register(UdonValueResolver resolver, ICollection<IValueConverter> container)
		{
			container.Add(new ArrayValueConverter(resolver));
		}
	}
}