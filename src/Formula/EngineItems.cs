using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Formula
{
    internal static class EngineItems
    {
		public static JsonSerializerSettings JsonSettings = new JsonSerializerSettings
		{
			ConstructorHandling = ConstructorHandling.Default,
			ContractResolver = new DefaultContractResolver(){NamingStrategy = new CamelCaseNamingStrategy() },
			Culture = CultureInfo.InvariantCulture,
			DateFormatHandling = DateFormatHandling.IsoDateFormat
		};
	}
}
