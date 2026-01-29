using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using ToolWire.Json;
using ToolWire.Utils;

namespace ToolWire.Tools
{
    public interface IToolRegistry
    {
        IReadOnlyList<Tool> Tools { get; }
        void Register(Delegate del, string? name = null, string? description = null);
        bool Unregister(string toolName);
        Tool? TryGet(string toolName);
    }

    /// <summary>
    /// Central registry that discovers, normalizes, and exposes LLM tools.
    /// This layer absorbs reflection, schema, and naming complexity.
    /// </summary>
    public sealed class ToolRegistry : IToolRegistry
    {
        private readonly ConcurrentDictionary<string, Tool> _registry = new(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyList<Tool> Tools => _registry.Values.ToArray();

        public void Register(
            Delegate del,
            string? name = null,
            string? description = null)
        {
            if (del == null) throw new ArgumentException(nameof(del));

            var method = del.Method;

            if (!IsMethodJsonCompatible(method))
                throw new InvalidOperationException(
                    $"Method is not JSON-compatible: {FormatMethod(method)}");

            var tool = CreateTool(
                method,
                del,
                explicitName: name,
                explicitDescription: description);

            if (_registry.ContainsKey(tool.Name))
                throw new InvalidOperationException(
                    $"Duplicate tool name '{tool.Name}'. " +
                    $"Already registered by {_registry[tool.Name].Executor.Method.DeclaringType?.FullName}." +
                    $"{_registry[tool.Name].Executor.Method.Name}, " +
                    $"conflicts with {tool.Executor.Method.DeclaringType?.FullName}." +
                    $"{tool.Executor.Method.Name}.");

            _registry[tool.Name] = tool;
        }
        public bool Unregister(string toolName)
        {
            if (string.IsNullOrWhiteSpace(toolName))
                throw new ArgumentException("Tool name is required.", nameof(toolName));

            return _registry.TryRemove(toolName, out _);
        }
        public Tool? TryGet(string toolName)
        {
            if (string.IsNullOrWhiteSpace(toolName))
                throw new ArgumentException("Tool name is required.", nameof(toolName));

            return _registry.TryGetValue(toolName, out var entry)
                ? entry
                : null;
        }

        #region internals
        private static Tool CreateTool(
            MethodInfo method,
            Delegate executor,
            string? explicitName = null,
            string? explicitDescription = null)
        {
            var declaringType = method.DeclaringType
                ?? throw new InvalidOperationException("Tool method has no declaring type.");

            var attr = method.GetCustomAttribute<ToolAttribute>();

            var rawName =
                !string.IsNullOrWhiteSpace(explicitName)
                    ? explicitName
                    : !string.IsNullOrWhiteSpace(attr?.Name)
                        ? attr!.Name!
                        : $"{declaringType.Name.ToSnake()}.{method.Name.ToSnake()}";

            var description =
                explicitDescription
                ?? attr?.Description
                ?? method.GetCustomAttribute<DescriptionAttribute>()?.Description
                ?? rawName;

            var parametersSchema = BuildParametersSchema(method);

            var tool = new Tool(rawName, description, parametersSchema, executor);
            return tool;
        }

        private static JObject BuildParametersSchema(MethodInfo method)
        {
            var properties = new JObject();
            var required = new JArray();

            foreach (var parameter in method.GetParameters())
            {
                if (parameter.ParameterType == typeof(CancellationToken))
                    continue;

                var paramName = parameter.Name
                    ?? throw new InvalidOperationException(
                        $"Unnamed parameter in method '{method.Name}'.");

                var schema = parameter.ParameterType.GetSchemaForType();

                var description =
                    parameter.GetCustomAttribute<DescriptionAttribute>()?.Description
                    ?? paramName;

                schema = new JsonSchemaBuilder(schema)
                    .Description(description)
                    .Build();

                properties[paramName] = schema;

                var isOptional =
                    parameter.HasDefaultValue ||
                    Nullable.GetUnderlyingType(parameter.ParameterType) != null;

                if (!isOptional)
                    required.Add(paramName);

            }

            return new JsonSchemaBuilder()
                .Type("object")
                .Properties(properties)
                .Required(required)
                .Build();
        }

        private static bool IsMethodJsonCompatible(MethodInfo method)
        {
            if (method.ContainsGenericParameters)
                return false;

            if (method.ReturnType.ContainsGenericParameters)
                return false;

            foreach (var p in method.GetParameters())
            {
                var t = p.ParameterType;

                if (t.IsByRef ||
                    t.IsPointer ||
                    t.ContainsGenericParameters)
                    return false;
            }

            return true;
        }

        private static string FormatMethod(MethodInfo m) =>
            $"{m.DeclaringType?.Name}.{m.Name}";

        #endregion
    }
}
