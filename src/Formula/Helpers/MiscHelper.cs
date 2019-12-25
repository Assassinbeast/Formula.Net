using Newtonsoft.Json;
using System.Linq;

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
		public static string GetFileNameWithoutExtension(string filePath)
		{
			var pathItems = filePath.Split('/', '\\'); // ["wwwroot", "foo", "hello.js.map"]
			var targetItem = pathItems[^1]; // hello.js.map
			var targetItemItems = targetItem.Split('.'); // ["hello", "js", "map"]
			return string.Join('.', targetItemItems.Take(targetItemItems.Length - 1));
		}
    }
}
