using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ToolWire.Tools;

namespace ToolWire.Providers.Mcp;

/// <summary>
/// Extension methods for registering ToolWire tools with MCP servers.
/// </summary>
public static class ToolWireMcpBuilderExtensions
{
    /// <summary>
    /// Adds all tools from a ToolWire registry to an MCP server builder.
    /// </summary>
    /// <param name="builder">The MCP server builder.</param>
    /// <param name="registry">The ToolWire tool registry.</param>
    /// <returns>The builder for chaining.</returns>
    public static IMcpServerBuilder WithToolWireTools(
        this IMcpServerBuilder builder,
        IToolRegistry registry)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        if (registry == null)
            throw new ArgumentNullException(nameof(registry));

        var executor = new ToolExecutor(registry);

        foreach (var tool in registry.Tools)
        {
            var mcpTool = new ToolWireMcpServerTool(tool, executor);
            builder.Services.AddSingleton(mcpTool);
        }

        return builder;
    }

    /// <summary>
    /// Adds a ToolWire executor's tools to an MCP server builder.
    /// </summary>
    /// <param name="builder">The MCP server builder.</param>
    /// <param name="executor">The ToolWire executor with registered tools.</param>
    /// <returns>The builder for chaining.</returns>
    public static IMcpServerBuilder WithToolWireTools(
        this IMcpServerBuilder builder,
        ToolExecutor executor)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        if (executor == null)
            throw new ArgumentNullException(nameof(executor));

        foreach (var tool in executor.Registry.Tools)
        {
            var mcpTool = new ToolWireMcpServerTool(tool, executor);
            builder.Services.AddSingleton(mcpTool);
        }

        return builder;
    }

    /// <summary>
    /// Creates an MCP server builder with ToolWire tools using stdio transport.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="registry">The ToolWire tool registry.</param>
    /// <param name="serverName">Optional server name. Defaults to "ToolWire-MCP-Server".</param>
    /// <returns>An MCP server builder configured with ToolWire tools.</returns>
    public static IMcpServerBuilder AddMcpServerWithToolWire(
        this IServiceCollection services,
        IToolRegistry registry,
        string serverName = "ToolWire-MCP-Server")
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        if (registry == null)
            throw new ArgumentNullException(nameof(registry));

        return services.AddMcpServer(options =>
        {
            options.ServerInfo = new Implementation
            {
                Name = serverName,
                Version = "1.0.0"
            };
            options.Capabilities = new ServerCapabilities
            {
                Tools = new ToolsCapability()
            };
        })
        .WithStdioServerTransport()
        .WithToolWireTools(registry);
    }

    /// <summary>
    /// Creates an MCP server builder with ToolWire tools using stdio transport.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="executor">The ToolWire executor with registered tools.</param>
    /// <param name="serverName">Optional server name. Defaults to "ToolWire-MCP-Server".</param>
    /// <returns>An MCP server builder configured with ToolWire tools.</returns>
    public static IMcpServerBuilder AddMcpServerWithToolWire(
        this IServiceCollection services,
        ToolExecutor executor,
        string serverName = "ToolWire-MCP-Server")
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        if (executor == null)
            throw new ArgumentNullException(nameof(executor));

        return services.AddMcpServer(options =>
        {
            options.ServerInfo = new Implementation
            {
                Name = serverName,
                Version = "1.0.0"
            };
            options.Capabilities = new ServerCapabilities
            {
                Tools = new ToolsCapability()
            };
        })
        .WithStdioServerTransport()
        .WithToolWireTools(executor);
    }
}
