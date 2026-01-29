using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ToolWire.Json;

namespace ToolWire.Tools
{
    /// <summary>
    /// Declares a method as an LLM-exposed tool.
    /// Name is optional and will be derived from the method name if omitted.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class ToolAttribute : Attribute
    {
        public string? Name { get; init; }
        public string? Description { get; init; }
    }

    /// <summary>
    /// Resolved, LLM-facing tool definition.
    /// This is the final contract sent to the model.
    /// </summary>
    public sealed class Tool
    {
        public string Name { get; }
        public string Description { get; }
        public JObject Parameters { get; }
        [JsonIgnore] internal Delegate Executor { get; }
        public Tool(string name, string? description, JObject parameters, Delegate executor)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Tool name is required.", nameof(name));

            Executor = executor ?? throw new ArgumentNullException(nameof(executor));

            Name = NormalizeName(name);
            Description = description ?? string.Empty;

            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));

            if (Parameters["type"]?.ToString() != "object")
                throw new ArgumentException(
                    "Tool parameters must be a JSON schema with type 'object'.",
                    nameof(parameters));
        }

        private static string NormalizeName(string name) =>
            name.Trim()
                .Replace(" ", "_")
                .ToLowerInvariant();

        /// <summary>
        /// LLM-facing signature used in prompts and planning.
        /// Stable and intentional.
        /// </summary>
        public string ForLlm()
        {
            var props = Parameters["properties"] as JObject;

            var args = props != null
                ? string.Join(", ", props.Properties().Select(p => p.Name))
                : string.Empty;

            var argPart = args.Length > 0 ? $"({args})" : "()";

            return string.IsNullOrWhiteSpace(Description)
                ? $"{Name}{argPart}"
                : $"{Name}{argPart} — {Description}";
        }

        /// <summary>
        /// Debug-only representation.
        /// Never rely on this for LLM interaction.
        /// </summary>
        public override string ToString()
        {
            var props = Parameters["properties"] as JObject;
            var count = props?.Count ?? 0;

            return $"Tool(Name={Name}, Params={count})";
        }
    }

    /// <summary>
    /// A tool invocation emitted by the LLM.
    /// This is a raw fact, not validated business input.
    /// </summary>
    public sealed class ToolCall
    {
        [JsonProperty("id")] public string Id { get; }
        [JsonProperty("name")] public string Name { get; }
        [JsonProperty("arguments")] public JObject Arguments { get; }
        [JsonConstructor]
        public ToolCall(string id, string name, JObject? arguments)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("ToolCall id is required.", nameof(id));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("ToolCall name is required.", nameof(name));

            Id = id;
            Name = name;
            Arguments = arguments ?? new JObject();
        }
        /// <summary>
        /// LLM-facing message describing the call.
        /// Used for reflection, retries, and self-correction.
        /// </summary>
        public string ForLlm()
        {
            var argsStr = Arguments.Count > 0
                ? string.Join(
                    ", ",
                    Arguments.Properties()
                        .Select(p => $"{p.Name}: {p.Value}"))
                : "none";

            return $"tool_call {Name} (id={Id}) with args [{argsStr}]";
        }

        /// <summary>
        /// Debug-only string.
        /// </summary>
        public override string ToString()
            => $"ToolCall(Name={Name}, Id={Id}, Args={Arguments.Count})";
    }

    /// <summary>
    /// Result of executing a tool call.
    /// Output is always textual and LLM-facing.
    /// </summary>
    public sealed class ToolResult
    {
        public string Id { get; }
        public object Output { get; }
        public bool IsError { get; }

        public ToolResult(
            string id,
            object output,
            bool isError = false)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Output = output ?? string.Empty;
            IsError = isError;
        }

        /// <summary>
        /// LLM-facing message — serialize on demand
        /// </summary>
        public string ForLlm() => Output.AsJsonString();

        public override string ToString()
            => $"ToolResult(Id={Id}, IsError={IsError})";
    }

}
