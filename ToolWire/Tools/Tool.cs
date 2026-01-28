using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace AgentCore.Tools
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ToolAttribute : Attribute
    {
        public string? Name { get; }
        public string? Description { get; }

        public ToolAttribute(string? name = null, string? description = null)
        {
            Name = name;
            Description = description;
        }
    }

    public class Tool
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public JObject ParametersSchema { get; set; }

        [JsonIgnore]
        public Delegate? Function { get; set; }

        public override string ToString()
        {
            var props = ParametersSchema?["properties"] as JObject;

            var args = props != null
                ? string.Join(", ", props.Properties().Select(p => p.Name))
                : "";

            var argPart = args.Length > 0 ? $"({args})" : "()";

            return !string.IsNullOrWhiteSpace(Description)
                ? $"{Name}{argPart} => {Description}"
                : $"{Name}{argPart}";
        }
    }

    public class ToolCall
    {
        [JsonConstructor]

        public ToolCall(string id, string name, JObject arguments, object[] parameters = null, string message = null)
        {
            Id = id;
            Name = name;
            Arguments = arguments ?? new JObject();
            Parameters = parameters;
            Message = message;
        }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; private set; }

        [JsonProperty("arguments")]
        public JObject Arguments { get; private set; }

        [JsonIgnore]
        public object[] Parameters { get; private set; } = Array.Empty<object>();

        [JsonIgnore]
        public string Message { get; set; }

        // message-only ctor
        public ToolCall(string message) : this(Guid.NewGuid().ToString(), "", new JObject())
        {
            Message = message;
        }

        public static ToolCall Empty { get; } = new ToolCall(Guid.NewGuid().ToString(), "", new JObject());

        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(Name) &&
            string.IsNullOrWhiteSpace(Message);

        public override string ToString()
        {
            var argsStr = Arguments != null && Arguments.Count > 0
                ? string.Join(", ", Arguments.Properties().Select(p => $"{p.Name}: {p.Value}"))
                : "none";

            return $"Name: '{Name}' (id: {Id}) with Arguments: [{argsStr}]";
        }
    }

    public sealed class ToolCallResult
    {
        public ToolCall Call { get; }
        public object? Result { get; }

        public ToolCallResult(ToolCall call, object? result)
        {
            Call = call;
            Result = result;
        }
    }
}
