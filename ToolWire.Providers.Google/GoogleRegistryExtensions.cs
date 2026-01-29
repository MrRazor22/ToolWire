using Newtonsoft.Json.Linq;
using Google.GenAI.Types;
using ToolWire.Tools;
using Newtonsoft.Json;
using Tool = Google.GenAI.Types.Tool;

namespace ToolWire.Providers.Google
{
    public static class GoogleRegistryExtensions
    {
        public static Tool ToGeminiTool(
            this IToolRegistry registry)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            return new Tool
            {
                FunctionDeclarations = registry.ToGeminiFunctions().ToList()
            };
        }

        public static IReadOnlyList<FunctionDeclaration> ToGeminiFunctions(
            this IToolRegistry registry)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            return registry.Tools.Select(t =>
            {
                var schema = t.Parameters ?? new JObject();

                if (schema["type"]?.ToString() != "object")
                {
                    schema = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = schema["properties"] ?? new JObject(),
                        ["required"] = schema["required"] ?? new JArray()
                    };
                }

                return new FunctionDeclaration
                {
                    Name = t.Name,
                    Description = t.Description,
                    Parameters = JsonConvert.DeserializeObject<Schema>(
                        schema.ToString(Formatting.None)
                    )!
                };
            }).ToList();
        }
    }
}
