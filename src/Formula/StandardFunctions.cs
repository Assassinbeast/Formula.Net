namespace Formula
{
	internal static class StandardFunctions
	{
		public static string StandardFullTitleGenerationFunc(string title)
		{
			if (string.IsNullOrWhiteSpace(title))
				return FormulaConfig.BaseTitle;
			return title + " - " + FormulaConfig.BaseTitle;
		}
	}
}
