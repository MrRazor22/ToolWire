using ToolWire.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ToolWire.Json
{
    public static class JsonExtensions
    {
        public static string AsJsonString(this object? obj)
        {
            return JsonConvert.SerializeObject(obj, Formatting.None);
        }

        public static string AsPrettyJson(this object? content)
        {
            if (content == null)
                return "<empty>";

            if (content is JToken jt)
                return jt.ToString(Formatting.Indented);

            if (content is ToolCall tc && tc.Arguments != null)
                return tc.Arguments.ToString(Formatting.Indented);

            return JsonConvert.SerializeObject(content, Formatting.Indented)
                   ?? "<unknown>";
        }

        public static bool TryParseCompleteJson(
            this string json,
            out JObject? result)
        {
            result = null;

            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                result = JObject.Parse(json);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        /// <summary>
        /// Finds all top-level JSON objects embedded in text.
        /// Used for extracting tool calls from LLM output.
        /// </summary>
        public static List<(int Start, int End, JObject Obj)>
            FindAllJsonObjects(this string content)
        {
            var results = new List<(int, int, JObject)>();

            if (string.IsNullOrEmpty(content))
                return results;

            int depth = 0;
            int start = -1;
            bool inString = false;
            bool escape = false;

            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];

                if (inString)
                {
                    if (escape)
                    {
                        escape = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escape = true;
                        continue;
                    }

                    if (c == '"')
                        inString = false;

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                {
                    if (depth == 0)
                        start = i;

                    depth++;
                    continue;
                }

                if (c == '}')
                {
                    depth--;

                    if (depth == 0 && start >= 0)
                    {
                        var jsonStr =
                            content.Substring(start, i - start + 1);

                        try
                        {
                            var obj = JObject.Parse(jsonStr);
                            results.Add((start, i, obj));
                        }
                        catch (JsonException)
                        {
                            // ignore malformed JSON fragments
                        }

                        start = -1;
                    }
                }
            }

            return results;
        }
    }
}
