using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ToolWire.Providers.Google.Json
{
    internal static class GeminiJsonExtensions
    {
        public static JObject AsJsonObject(this object? obj)
        {
            if (obj == null)
                return new JObject();

            if (obj is JObject jo)
                return jo;

            if (obj is string s)
                return new JObject { ["value"] = s };

            if (obj.GetType().IsPrimitive)
                return new JObject { ["value"] = JToken.FromObject(obj) };

            var token = JToken.Parse(
                JsonConvert.SerializeObject(obj, Formatting.None)
            );

            return token as JObject
                ?? new JObject { ["value"] = token };
        }
    }
}
