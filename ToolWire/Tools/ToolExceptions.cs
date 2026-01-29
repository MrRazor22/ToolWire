using ToolWire.Json;
using ToolWire.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ToolWire.Tools
{
    /// <summary>
    /// Base exception for all tool-related failures.
    /// Message MUST be safe to surface directly to the LLM.
    /// </summary>
    public class ToolException : Exception
    {
        public string ToolName { get; }

        public ToolException(
            string toolName,
            string message,
            Exception? innerException = null)
            : base(message, innerException)
        {
            if (string.IsNullOrWhiteSpace(toolName))
                throw new ArgumentException("Tool name is required.", nameof(toolName));

            ToolName = toolName;
        }

        /// <summary>
        /// Message intended to be sent back to the LLM for self-correction.
        /// Must be stable, concise, and tool-facing.
        /// </summary>
        public virtual string ForLlm()
            => Message;

        public override string ToString()
            => $"{ToolName}: {Message}";
    }


    /// <summary>
    /// Thrown when multiple tool parameters fail schema validation.
    /// Aggregates individual validation messages into a single LLM-safe message.
    /// </summary>
    internal sealed class ToolValidationAggregateException : ToolException
    {
        public IReadOnlyList<SchemaValidationError> Errors { get; }

        public ToolValidationAggregateException(
            string toolName,
            IEnumerable<SchemaValidationError>? errors)
            : base(
                toolName,
                BuildMessage(errors),
                new ArgumentException("Tool parameter validation failed"))
        {
            Errors = (errors ?? Enumerable.Empty<SchemaValidationError>()).ToList();
        }

        public override string ForLlm()
            => $"Invalid arguments for '{ToolName}': {Message}";

        private static string BuildMessage(IEnumerable<SchemaValidationError>? errors)
        {
            var list = errors?.ToList() ?? new List<SchemaValidationError>();

            return list.Count == 0
                ? "Invalid tool parameters."
                : list.Select(e => e.Message).ToJoinedString("; ");
        }
    }


    /// <summary>
    /// Thrown when a single tool parameter fails validation.
    /// </summary>
    internal sealed class ToolValidationException : ToolException
    {
        public string ParameterName { get; }

        public ToolValidationException(
            string toolName,
            string parameterName,
            string message)
            : base(
                toolName,
                $"{parameterName}: {message}",
                new ArgumentException(message))
        {
            ParameterName = parameterName;
        }

        public override string ForLlm()
            => $"Invalid '{ParameterName}' for '{ToolName}': {Message}";
    }
}
