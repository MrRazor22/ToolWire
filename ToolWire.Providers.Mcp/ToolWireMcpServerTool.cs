using McpProtocol = ModelContextProtocol.Protocol;
using McpServer = ModelContextProtocol.Server;
using Newtonsoft.Json.Linq;

namespace ToolWire.Providers.Mcp;

/// <summary>
/// An MCP server tool that wraps a ToolWire tool and executor.
/// </summary>
public sealed class ToolWireMcpServerTool : McpServer.McpServerTool
{
    private readonly ToolWire.Tools.Tool _toolWireTool;
    private readonly ToolWire.Tools.ToolExecutor _executor;
    private readonly McpProtocol.Tool? _protocolTool;

    /// <summary>
    /// Creates a new MCP server tool from a ToolWire tool.
    /// </summary>
    /// <param name="toolWireTool">The ToolWire tool to wrap.</param>
    /// <param name="executor">The ToolWire executor.</param>
    public ToolWireMcpServerTool(ToolWire.Tools.Tool toolWireTool, ToolWire.Tools.ToolExecutor executor)
    {
        _toolWireTool = toolWireTool ?? throw new ArgumentNullException(nameof(toolWireTool));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _protocolTool = toolWireTool.ToMcpTool();
    }

    /// <inheritdoc />
    public override McpProtocol.Tool ProtocolTool => _protocolTool!;

    /// <inheritdoc />
    public override IReadOnlyList<object> Metadata => new List<object>();

    /// <inheritdoc />
    public override async ValueTask<McpProtocol.CallToolResult> InvokeAsync(
        McpServer.RequestContext<McpProtocol.CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        if (request.Params?.Arguments == null)
        {
            return new McpProtocol.CallToolResult
            {
                Content = new List<McpProtocol.ContentBlock>
                {
                    new McpProtocol.TextContentBlock { Text = "Error: No arguments provided" }
                },
                IsError = true
            };
        }

        // Convert MCP arguments to ToolWire format
        var args = ConvertArguments(request.Params.Arguments);

        var toolCall = new ToolWire.Tools.ToolCall(
            id: Guid.NewGuid().ToString(),
            name: _toolWireTool.Name,
            arguments: args
        );

        try
        {
            var result = await _executor.ExecuteAsync(toolCall, cancellationToken);
            return result.ToMcpResult();
        }
        catch (Exception ex)
        {
            return new McpProtocol.CallToolResult
            {
                Content = new List<McpProtocol.ContentBlock>
                {
                    new McpProtocol.TextContentBlock { Text = $"Error executing tool: {ex.Message}" }
                },
                IsError = true
            };
        }
    }

        private static JObject ConvertArguments(IDictionary<string, System.Text.Json.JsonElement> arguments)
    {
        var jObject = new JObject();

        foreach (var kvp in arguments)
        {
            jObject[kvp.Key] = ConvertJsonElement(kvp.Value);
        }

        return jObject;
    }

    private static JToken ConvertJsonElement(System.Text.Json.JsonElement element)
    {
        switch (element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.String:
                return element.GetString()!;
            case System.Text.Json.JsonValueKind.Number:
                if (element.TryGetInt32(out var intValue))
                    return intValue;
                if (element.TryGetInt64(out var longValue))
                    return longValue;
                if (element.TryGetDouble(out var doubleValue))
                    return doubleValue;
                return element.GetRawText();
            case System.Text.Json.JsonValueKind.True:
                return true;
            case System.Text.Json.JsonValueKind.False:
                return false;
            case System.Text.Json.JsonValueKind.Null:
                return JValue.CreateNull();
            case System.Text.Json.JsonValueKind.Object:
                var obj = new JObject();
                foreach (var prop in element.EnumerateObject())
                {
                    obj[prop.Name] = ConvertJsonElement(prop.Value);
                }
                return obj;
            case System.Text.Json.JsonValueKind.Array:
                var arr = new JArray();
                foreach (var item in element.EnumerateArray())
                {
                    arr.Add(ConvertJsonElement(item));
                }
                return arr;
            default:
                return element.GetRawText();
        }
    }
}
