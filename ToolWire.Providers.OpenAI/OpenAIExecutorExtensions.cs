#pragma warning disable OPENAI001

using ToolWire.Tools;
using Newtonsoft.Json.Linq;
using OpenAI.Responses;

namespace ToolWire.Providers.OpenAI
{
    public static class OpenAIExecutorExtensions
    {
        public static async Task<FunctionCallOutputResponseItem> ExecuteAsync(
            this ToolExecutor executor,
            FunctionCallResponseItem functionCall,
            CancellationToken ct = default)
        {
            if (executor == null)
                throw new ArgumentNullException(nameof(executor));
            if (functionCall == null)
                throw new ArgumentNullException(nameof(functionCall));

            var json = functionCall.FunctionArguments?.ToString();
            var args = string.IsNullOrWhiteSpace(json)
                ? new JObject()
                : JObject.Parse(json);

            var call = new ToolCall(
                functionCall.CallId,
                functionCall.FunctionName,
                args
            );

            var result = await executor.ExecuteAsync(call, ct);

            return new FunctionCallOutputResponseItem(
                result.Id,
                result.ForLlm()
            );
        }
    }
}
