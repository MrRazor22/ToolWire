using ToolWire.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace ToolWire.Tools
{
    public static class ToolCallExtensions
    {
        /// <summary>
        /// Turns the tool-call arguments into a consistent JSON string
        /// </summary>
        public static string NormalizeToolArgs(this ToolCall call)
            => call?.Arguments == null
                ? "{}"
                : Canonicalize(call.Arguments).ToString(Formatting.None);

        private static readonly Regex SpaceRegex =
            new(@"\s+", RegexOptions.Compiled);

        private static JToken Canonicalize(JToken? node)
        {
            switch (node)
            {
                case JObject obj:
                    var newObj = new JObject();
                    foreach (var kvp in obj.Properties()
                                           .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        newObj[kvp.Name] = Canonicalize(kvp.Value);
                    }
                    return newObj;

                case JArray arr:
                    var newArr = new JArray();
                    foreach (var item in arr)
                    {
                        newArr.Add(Canonicalize(item));
                    }
                    return newArr;

                case JValue val when val.Type == JTokenType.String:
                    var s = val.Value<string>() ?? string.Empty;
                    s = SpaceRegex.Replace(s.Trim(), " ");
                    return JValue.CreateString(s);

                default:
                    return node?.DeepClone() ?? JValue.CreateNull();
            }
        }

        /// <summary>
        /// Tries to extract a tool call from inline LLM text output.
        /// Returns any prefix text and the matched tool call.
        /// </summary>
        public static (string? Prefix, ToolCall? ToolCall)
            TryExtractToolCall(
                this string content,
                IToolRegistry registry)
        {
            if (string.IsNullOrWhiteSpace(content))
                return (null, null);

            foreach (var (start, _, obj) in content.FindAllJsonObjects())
            {
                if (obj.Type != JTokenType.Object)
                    continue;

                var name = obj["name"]?.ToString();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                // Only accept registered tools
                if (registry.TryGet(name) == null)
                    continue;

                if (obj["arguments"] is not JObject args)
                    continue;

                var id = obj["id"]?.ToString()
                         ?? Guid.NewGuid().ToString();

                var prefix = start > 0
                    ? content.Substring(0, start)
                    : null;

                return (
                    prefix,
                    new ToolCall(
                        id,
                        name,
                        (JObject)args.DeepClone()
                    )
                );
            }

            return (null, null);
        }
    }
}
