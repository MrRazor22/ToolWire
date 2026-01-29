#pragma warning disable OPENAI001

using Newtonsoft.Json.Linq;
using OpenAI.Responses;
using ToolWire.Tools;

namespace ToolWire.Providers.OpenAI
{
    public static class OpenAIRegistryExtensions
    {
        public static CreateResponseOptions UseToolWire(
        this CreateResponseOptions options,
        IToolExecutor executor)
        {
            foreach (var tool in executor.Registry.ToOpenAITools())
                options.Tools.Add(tool);

            return options;
        }
        public static IReadOnlyList<FunctionTool> ToOpenAITools(
            this IToolRegistry registry)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            return registry.Tools.Select(t =>
            {
                // Ensure OpenAI-compliant root schema
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

                return ResponseTool.CreateFunctionTool(
                    functionName: t.Name,
                    functionDescription: t.Description,
                    functionParameters: BinaryData.FromString(
                        schema.ToString(Newtonsoft.Json.Formatting.None)
                    ),
                    strictModeEnabled: false
                );
            }).ToList();
        }
    }
}
