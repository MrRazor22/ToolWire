using ToolWire.Tools;
using Newtonsoft.Json.Linq;
using Google.GenAI.Types;
using Newtonsoft.Json;
using System.Text.Json;
using ToolWire.Json;
using ToolWire.Providers.Google.Json;

namespace ToolWire.Providers.Google
{
    public static class GoogleExecutorExtensions
    {
        public static async Task<Part> ExecuteAsync(
            this ToolExecutor executor,
            FunctionCall functionCall,
            CancellationToken ct = default)
        {
            if (executor == null)
                throw new ArgumentNullException(nameof(executor));
            if (functionCall == null)
                throw new ArgumentNullException(nameof(functionCall));

            var args = functionCall.Args == null
                ? new JObject()
                : JObject.Parse(
                    functionCall.Args.AsJsonString()
                  );

            var call = new ToolCall(
                id: Guid.NewGuid().ToString(),
                name: functionCall.Name!,
                arguments: args
            );

            var result = await executor.ExecuteAsync(call, ct);
            var responseDict =
                   result.Output.AsJsonObject()
                               .ToObject<Dictionary<string, object>>()!;

            return new Part
            {
                FunctionResponse = new FunctionResponse
                {
                    Name = functionCall.Name,
                    Response = responseDict
                }
            };
        }
    }
}
