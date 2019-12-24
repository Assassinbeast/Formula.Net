namespace Formula.Helpers
{
    internal static class HttpHelper
    {
		public static void SetNoCacheOnResponseHeaders(Microsoft.AspNetCore.Http.IHeaderDictionary headers)
		{
			headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
			headers["Pragma"] = "no-cache";
			headers["Expires"] = "0";
		}
	}
}
