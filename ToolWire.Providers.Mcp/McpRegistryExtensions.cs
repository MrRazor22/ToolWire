using Newtonsoft.Json.Linq;
using ToolWire.Tools;
using ToolWire.Json;
using McpTool = ModelContextProtocol.Protocol.Tool;
using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace ToolWire.Providers.Mcp;

/// <summary>
/// Extension methods for converting ToolWire tool registries to MCP format.
/// </summary>
public static class McpRegistryExtensions
{
    /// <summary>
    /// Converts all tools in the registry to MCP protocol tools.
    /// </summary>
    /// <param name="registry">The ToolWire registry.</param>
    /// <returns>A list of MCP protocol tools.</returns>
    public static IReadOnlyList<McpTool> ToMcpTools(this IToolRegistry registry)
    {
        if (registry == null)
            throw new ArgumentNullException(nameof(registry));

        return registry.Tools.Select(t => t.ToMcpTool()).ToList();
    }

    /// <summary>
    /// Converts a single ToolWire tool to an MCP protocol tool.
    /// </summary>
    /// <param name="tool">The ToolWire tool.</param>
    /// <returns>An MCP protocol tool.</returns>
    public static McpTool ToMcpTool(this ToolWire.Tools.Tool tool)
    {
        if (tool == null)
            throw new ArgumentNullException(nameof(tool));

        // Convert Newtonsoft.Json schema to System.Text.Json
        var schemaJson = tool.Parameters?.ToString() ?? "{\"type\": \"object\"}";
        using var doc = JsonDocument.Parse(schemaJson);

        return new McpTool
        {
            Name = tool.Name,
            Description = tool.Description,
            InputSchema = doc.RootElement.Clone()
        };
    }

    /// <summary>
    /// Converts tool execution results to MCP CallToolResult format.
    /// </summary>
    /// <param name="result">The ToolWire execution result.</param>
    /// <returns>An MCP CallToolResult.</returns>
    public static ModelContextProtocol.Protocol.CallToolResult ToMcpResult(this ToolResult result)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        var content = new List<ContentBlock>();

        if (result.IsError)
        {
            content.Add(new TextContentBlock { Text = result.Output?.ToString() ?? "" });

            return new ModelContextProtocol.Protocol.CallToolResult
            {
                Content = content,
                IsError = true
            };
        }

        // Try to parse as JSON for structured content
        try
        {
            var jsonObj = JObject.Parse(result.Output?.ToString() ?? "");
            var jsonStr = jsonObj.ToString(Newtonsoft.Json.Formatting.None);
            
            content.Add(new TextContentBlock { Text = jsonStr });
        }
        catch
        {
            // Not valid JSON, treat as plain text
            content.Add(new TextContentBlock { Text = result.Output?.ToString() ?? "" });
        }

        return new ModelContextProtocol.Protocol.CallToolResult
        {
            Content = content,
            IsError = false
        };
    }
}
