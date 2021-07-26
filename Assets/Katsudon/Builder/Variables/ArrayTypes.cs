using System;

namespace Katsudon.Builder.Variables
{
	[VariableBuilder]
	public class ArrayTypes : IVariableBuilder
	{
		int IVariableBuilder.order => 150;

		private VariableBuildersCollection collection;

		private ArrayTypes(VariableBuildersCollection collection)
		{
			this.collection = collection;
		}

		bool IVariableBuilder.TryBuildVariable(IVariable variable, VariablesTable table)
		{
			var type = variable.type;
			if(type.IsArray && !Utils.IsUdonType(type))
			{
				var udonType = GetUdonArrayType(type);
				if(udonType != type)
				{
					if(variable is ISignificantVariable significant)
					{
						var value = significant.value;
						if(value != null && !udonType.IsAssignableFrom(value.GetType()))
						{
							TryConvert(udonType, ref value);
						}
						table.AddVariable(TypedSignificantVariable.From(variable, udonType, value));
					}
					else
					{
						table.AddVariable(TypedVariable.From(variable, udonType));
					}
					return true;
				}
			}
			return false;
		}

		public bool TryConvert(Type type, ref object value)
		{
			if(type.IsArray && !Utils.IsUdonType(type))
			{
				if(type.GetArrayRank() <= 1)
				{
					var array = (Array)value;
					var elementType = type.GetElementType();
					var newArray = Array.CreateInstance(elementType, array.Length);
					for(var i = 0; i < array.Length; i++)
					{
						newArray.SetValue(collection.Convert(elementType, array.GetValue(i)), i);
					}
					value = newArray;
				}
				else
				{
					var array = (Array)value;
					int[] lengths = new int[array.Rank];
					int[] indices = new int[array.Rank];
					var elementType = type.GetElementType();
					var newArray = Array.CreateInstance(elementType, lengths);

					bool breakLoop = false;
					while(true)
					{
						newArray.SetValue(collection.Convert(elementType, array.GetValue(indices)), indices);
						for(int i = indices.Length - 1; i >= 0; i--)
						{
							indices[i]++;
							if(indices[i] < lengths[i]) break;
							if(i == 0)
							{
								breakLoop = true;
								break;
							}
							indices[i] = 0;
						}
						if(breakLoop) break;
					}
					value = newArray;
				}
				return true;
			}
			return false;
		}

		public static Type GetUdonArrayType(Type arrayType)
		{
			var rank = arrayType.GetArrayRank();
			if(rank <= 1)
			{
				if(Utils.IsUdonType(arrayType))
				{
					return arrayType;
				}
				else
				{
					//FIX: cache
					// Finding the closest array type
					var parentType = arrayType.GetElementType().BaseType;
					while(parentType != null)
					{
						arrayType = parentType.MakeArrayType();
						if(Utils.IsUdonType(arrayType)) return arrayType;
						parentType = parentType.BaseType;
					}
				}
				return typeof(object[]);
			}
			return typeof(Array);
		}

		public static void Register(VariableBuildersCollection container, IModulesContainer modules)
		{
			container.AddBuilder(new ArrayTypes(container));
		}
	}
}