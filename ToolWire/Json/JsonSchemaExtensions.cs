using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace ToolWire.Json
{
    public sealed record SchemaValidationError(
        string Param,
        string? Path,
        string Message,
        string ErrorType
    );

    public static class JsonSchemaExtensions
    {
        private static readonly ConcurrentDictionary<Type, JObject> _schemaCache = new();

        public static JObject GetSchemaFor<T>() =>
            typeof(T).GetSchemaForType();

        public static JObject GetSchemaForType(this Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            var cached = _schemaCache.GetOrAdd(
                type,
                t => BuildSchema(t, new HashSet<Type>())
            );

            return (JObject)cached.DeepClone();
        }

        // ================= BUILD =================

        private static JObject BuildSchema(Type type, HashSet<Type> visited)
        {
            // ---- enum ----
            if (type.IsEnum)
            {
                var names = Enum.GetNames(type);

                return new JsonSchemaBuilder()
                    .Type("string")
                    .Enum(names)
                    .Description(
                        type.GetCustomAttribute<DescriptionAttribute>()?.Description
                        ?? $"One of: {string.Join(", ", names)}")
                    .Build();
            }

            // ---- simple ----
            if (type.IsSimpleType())
            {
                var builder = new JsonSchemaBuilder()
                    .Type(type.MapClrTypeToJsonType());

                if (type == typeof(DateTime))
                    builder.Format("date-time");

                return builder.Build();
            }

            // ---- array ----
            if (type.IsArray)
            {
                return new JsonSchemaBuilder()
                    .Type("array")
                    .Items(type.GetElementType()!.GetSchemaForType())
                    .Build();
            }

            // ---- IEnumerable<T> ----
            if (typeof(IEnumerable).IsAssignableFrom(type) &&
                type.IsGenericType &&
                type != typeof(string))
            {
                return new JsonSchemaBuilder()
                    .Type("array")
                    .Items(type.GetGenericArguments()[0].GetSchemaForType())
                    .Build();
            }

            // ---- Dictionary<string,T> ----
            if (type.IsGenericType &&
                type.GetGenericTypeDefinition() == typeof(Dictionary<,>) &&
                type.GetGenericArguments()[0] == typeof(string))
            {
                return new JsonSchemaBuilder()
                    .Type("object")
                    .AdditionalProperties(
                        type.GetGenericArguments()[1].GetSchemaForType())
                    .Build();
            }

            // ---- recursion guard ----
            if (!visited.Add(type))
            {
                return new JsonSchemaBuilder()
                    .Type("object")
                    .Build();
            }

            var props = new JObject();
            var required = new JArray();

            foreach (var prop in type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.GetCustomAttribute<
                    System.Text.Json.Serialization.JsonIgnoreAttribute>() != null)
                    continue;

                var propType =
                    Nullable.GetUnderlyingType(prop.PropertyType)
                    ?? prop.PropertyType;

                var builder = new JsonSchemaBuilder(
                    propType.GetSchemaForType());

                // description
                if (prop.GetCustomAttribute<DescriptionAttribute>() is { } desc &&
                    !string.IsNullOrWhiteSpace(desc.Description))
                {
                    builder.Description(desc.Description);
                }

                // email
                if (prop.GetCustomAttribute<EmailAddressAttribute>() != null)
                    builder.Format("email");

                // string length
                if (prop.GetCustomAttribute<StringLengthAttribute>() is { } len)
                {
                    if (len.MinimumLength > 0)
                        builder.MinLength(len.MinimumLength);

                    if (len.MaximumLength > 0)
                        builder.MaxLength(len.MaximumLength);
                }

                // regex
                if (prop.GetCustomAttribute<RegularExpressionAttribute>() is { } rx)
                    builder.Pattern(rx.Pattern);

                // numeric range
                if (prop.GetCustomAttribute<RangeAttribute>() is { } range)
                {
                    if (TryToDouble(range.Minimum, out var min))
                        builder.Minimum(min);

                    if (TryToDouble(range.Maximum, out var max))
                        builder.Maximum(max);
                }

                props[prop.Name] = builder.Build();

                if (prop.GetCustomAttribute<RequiredAttribute>() != null ||
                    !prop.IsOptional())
                {
                    required.Add(prop.Name);
                }
            }

            return new JsonSchemaBuilder()
                .Type("object")
                .Properties(props)
                .Required(required)
                .AdditionalProperties(false)
                .Build();
        }

        // ================= OPTIONAL =================

        private static bool IsOptional(this PropertyInfo prop)
        {
            if (Nullable.GetUnderlyingType(prop.PropertyType) != null)
                return true;

            return IsNullableReference(prop);
        }

        private static bool IsNullableReference(PropertyInfo prop)
        {
            var attr = prop.CustomAttributes
                .FirstOrDefault(a => a.AttributeType.Name == "NullableAttribute");

            if (attr?.ConstructorArguments.Count > 0)
            {
                var arg = attr.ConstructorArguments[0];

                if (arg.Value is byte b)
                    return b == 2;

                if (arg.Value is IReadOnlyCollection<CustomAttributeTypedArgument> arr &&
                    arr.Count > 0 &&
                    arr.First().Value is byte bb)
                {
                    return bb == 2;
                }
            }

            return false;
        }

        // ================= TYPE HELPERS =================

        public static bool IsSimpleType(this Type type) =>
            type.IsPrimitive ||
            type == typeof(string) ||
            type == typeof(decimal) ||
            type == typeof(DateTime) ||
            type == typeof(Guid);

        public static string MapClrTypeToJsonType(this Type type)
        {
            if (Nullable.GetUnderlyingType(type) is Type u)
                type = u;

            if (type.IsEnum) return "string";
            if (type == typeof(string) || type == typeof(char)) return "string";
            if (type == typeof(bool)) return "boolean";
            if (type == typeof(float) ||
                type == typeof(double) ||
                type == typeof(decimal)) return "number";
            if (type.IsIntegerType()) return "integer";
            if (type.IsArray ||
                (typeof(IEnumerable).IsAssignableFrom(type) &&
                 type != typeof(string)))
                return "array";

            return "object";
        }

        private static bool IsIntegerType(this Type t) =>
            t == typeof(int) || t == typeof(long) ||
            t == typeof(short) || t == typeof(byte) ||
            t == typeof(uint) || t == typeof(ulong) ||
            t == typeof(ushort) || t == typeof(sbyte);

        private static bool TryToDouble(object? value, out double result)
        {
            if (value == null)
            {
                result = default;
                return false;
            }

            return double.TryParse(value.ToString(), out result);
        }

        // ================= VALIDATION =================

        public static List<SchemaValidationError> Validate(
            this JObject schema,
            JToken? node,
            string path = "")
        {
            var errors = new List<SchemaValidationError>();

            if (node == null)
                return errors;

            var type = schema["type"]?.ToString();

            switch (type)
            {
                case "string" when node.Type != JTokenType.String:
                    errors.Add(new(path, path, "Expected string", "type"));
                    break;

                case "integer" when node.Type != JTokenType.Integer:
                    errors.Add(new(path, path, "Expected integer", "type"));
                    break;

                case "number" when node.Type != JTokenType.Float &&
                                   node.Type != JTokenType.Integer:
                    errors.Add(new(path, path, "Expected number", "type"));
                    break;

                case "boolean" when node.Type != JTokenType.Boolean:
                    errors.Add(new(path, path, "Expected boolean", "type"));
                    break;

                case "array" when node is JArray arr &&
                                  schema["items"] is JObject itemSchema:
                    for (int i = 0; i < arr.Count; i++)
                        errors.AddRange(
                            itemSchema.Validate(arr[i], $"{path}[{i}]"));
                    break;

                case "object" when node is JObject obj &&
                                   schema["properties"] is JObject props:
                    foreach (var kv in props)
                        if (obj.TryGetValue(kv.Key, out var val) && kv.Value is JObject valueSchema)
                            errors.AddRange(
                                valueSchema.Validate(val, $"{path}.{kv.Key}".Trim('.')));
                    break;
            }

            return errors;
        }
    }
}
