using Newtonsoft.Json;

namespace Formula.Helpers
{
    internal static class MiscHelper
    {
		
        public static string ToJson(this object obj, Formatting formatting = Formatting.None)
		{
			return JsonConvert.SerializeObject(obj, formatting, EngineItems.JsonSettings);
		}
		public static T ToObject<T>(this string json)
		{
			return JsonConvert.DeserializeObject<T>(json, EngineItems.JsonSettings);
		}
		public static string GetFileNameWithoutExt(string filePath)
		{
			//Get hello/foo.js
			var pathItems = filePath.Replace('\\', '/').Split('/');
			return pathItems[pathItems.Length - 1].Split('.')[0];
		}
    }
}
