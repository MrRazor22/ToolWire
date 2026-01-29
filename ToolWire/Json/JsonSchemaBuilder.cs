using Newtonsoft.Json.Linq;

namespace ToolWire.Json
{
    internal static class JsonSchemaConstants
    {
        public const string Type = "type";
        public const string Properties = "properties";
        public const string Required = "required";
        public const string Description = "description";

        public const string Enum = "enum";
        public const string Const = "const";
        public const string Format = "format";

        public const string MinLength = "minLength";
        public const string MaxLength = "maxLength";
        public const string Pattern = "pattern";

        public const string Minimum = "minimum";
        public const string Maximum = "maximum";

        public const string Items = "items";
        public const string MinItems = "minItems";
        public const string MaxItems = "maxItems";

        public const string AdditionalProperties = "additionalProperties";

        public const string AnyOf = "anyOf";
        public const string OneOf = "oneOf";
        public const string AllOf = "allOf";
    }

    /// <summary>
    /// Minimal, defensive JSON Schema builder.
    /// Produces object-safe schemas for tool input validation.
    /// </summary>
    public sealed class JsonSchemaBuilder
    {
        private readonly JObject _schema;

        public JsonSchemaBuilder()
        {
            _schema = new JObject();
        }

        public JsonSchemaBuilder(JObject existing)
        {
            _schema = existing != null
                ? (JObject)existing.DeepClone()
                : new JObject();
        }

        public JsonSchemaBuilder Type(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
                throw new ArgumentException("Schema type is required.", nameof(type));

            _schema[JsonSchemaConstants.Type] = type;
            return this;
        }

        public JsonSchemaBuilder Type<T>()
        {
            _schema[JsonSchemaConstants.Type] =
                typeof(T).MapClrTypeToJsonType();
            return this;
        }

        public JsonSchemaBuilder Properties(JObject properties)
        {
            _schema[JsonSchemaConstants.Properties] =
                properties ?? throw new ArgumentNullException(nameof(properties));
            return this;
        }

        public JsonSchemaBuilder Items(JToken schema)
        {
            _schema[JsonSchemaConstants.Items] =
                schema ?? throw new ArgumentNullException(nameof(schema));
            return this;
        }

        public JsonSchemaBuilder Required(JArray required)
        {
            if (required == null || required.Count == 0)
                return this;

            var cleaned = required
                .OfType<JValue>()
                .Where(v => v.Type == JTokenType.String)
                .Select(v => (string)v!)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (cleaned.Length > 0)
                _schema[JsonSchemaConstants.Required] = new JArray(cleaned);

            return this;
        }

        public JsonSchemaBuilder Description(string description)
        {
            if (!string.IsNullOrWhiteSpace(description))
                _schema[JsonSchemaConstants.Description] = description;

            return this;
        }

        public JsonSchemaBuilder Enum(params string[] values)
        {
            if (values == null || values.Length == 0)
                return this;

            var cleaned = values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (cleaned.Length > 0)
                _schema[JsonSchemaConstants.Enum] = new JArray(cleaned);

            return this;
        }

        public JsonSchemaBuilder Const(JToken value)
        {
            _schema[JsonSchemaConstants.Const] =
                value ?? throw new ArgumentNullException(nameof(value));
            return this;
        }

        public JsonSchemaBuilder Format(string format)
        {
            if (!string.IsNullOrWhiteSpace(format))
                _schema[JsonSchemaConstants.Format] = format;

            return this;
        }

        public JsonSchemaBuilder MinLength(int value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            _schema[JsonSchemaConstants.MinLength] = value;
            return this;
        }

        public JsonSchemaBuilder MaxLength(int value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            _schema[JsonSchemaConstants.MaxLength] = value;
            return this;
        }

        public JsonSchemaBuilder MinItems(int value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            _schema[JsonSchemaConstants.MinItems] = value;
            return this;
        }

        public JsonSchemaBuilder MaxItems(int value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            _schema[JsonSchemaConstants.MaxItems] = value;
            return this;
        }

        public JsonSchemaBuilder Pattern(string regex)
        {
            if (!string.IsNullOrWhiteSpace(regex))
                _schema[JsonSchemaConstants.Pattern] = regex;

            return this;
        }

        public JsonSchemaBuilder Minimum(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value));

            _schema[JsonSchemaConstants.Minimum] = value;
            return this;
        }

        public JsonSchemaBuilder Maximum(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value));

            _schema[JsonSchemaConstants.Maximum] = value;
            return this;
        }

        public JsonSchemaBuilder AdditionalProperties(bool allow)
        {
            _schema[JsonSchemaConstants.AdditionalProperties] = allow;
            return this;
        }

        public JsonSchemaBuilder AdditionalProperties(JObject schema)
        {
            _schema[JsonSchemaConstants.AdditionalProperties] =
                schema ?? throw new ArgumentNullException(nameof(schema));
            return this;
        }

        public JsonSchemaBuilder AnyOf(params JToken[] schemas)
        {
            AddComposite(JsonSchemaConstants.AnyOf, schemas);
            return this;
        }

        public JsonSchemaBuilder OneOf(params JToken[] schemas)
        {
            AddComposite(JsonSchemaConstants.OneOf, schemas);
            return this;
        }

        public JsonSchemaBuilder AllOf(params JToken[] schemas)
        {
            AddComposite(JsonSchemaConstants.AllOf, schemas);
            return this;
        }

        public JObject Build()
            => (JObject)_schema.DeepClone();

        private void AddComposite(string key, JToken[] schemas)
        {
            if (schemas == null || schemas.Length == 0)
                return;

            var cleaned = schemas.Where(s => s != null).ToArray();
            if (cleaned.Length > 0)
                _schema[key] = new JArray(cleaned);
        }
    }
}
