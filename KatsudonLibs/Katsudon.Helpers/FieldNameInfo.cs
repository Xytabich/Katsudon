namespace Katsudon.Builder.Helpers
{
	public struct FieldNameInfo
	{
		public string getterName;
		public string setterName;

		public FieldNameInfo(string getterName, string setterName)
		{
			this.getterName = getterName;
			this.setterName = setterName;
		}
	}
}