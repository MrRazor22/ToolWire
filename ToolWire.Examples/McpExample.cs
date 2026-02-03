using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ToolWire.Tools;
using ToolWire.Providers.Mcp;

namespace ToolWire.Examples;

/// <summary>
/// REAL MCP Integration Example - Using actual ModelContextProtocol NuGet package
/// 
/// This demonstrates:
/// 1. Creating an MCP server with ToolWire tools
/// 2. Starting the server with stdio transport
/// 3. The server runs and responds to MCP protocol messages
/// 
/// To test this, you would connect an MCP client to this process via stdio.
/// For demo purposes, we show the setup and tool registration working.
/// </summary>
public static class McpExample
{
    public static async Task RunAsync()
    {
        Console.WriteLine("=== ToolWire + REAL MCP Integration ===\n");
        Console.WriteLine("This example creates an actual MCP server with ToolWire tools!\n");

        // STEP 1: Register tools with ToolWire (the easy part)
        Console.WriteLine("1. Registering tools with ToolWire...");
        
        var registry = new ToolRegistry();
        
        // Register tools using delegates
        registry.Register((double a, double b) => a + b, 
            name: "add", 
            description: "Adds two numbers together");
        
        registry.Register((double a, double b) => a * b, 
            name: "multiply", 
            description: "Multiplies two numbers");
        
        registry.Register((double baseValue, double exponent) => Math.Pow(baseValue, exponent), 
            name: "power", 
            description: "Calculates base raised to the power of exponent");
        
        var weatherService = new WeatherService();
        registry.Register((string city) => weatherService.GetWeather(city), 
            name: "get_weather", 
            description: "Gets the current weather for a city");
        
        registry.Register((string text) => text?.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length ?? 0, 
            name: "word_count", 
            description: "Counts the number of words in text");
        
        registry.Register((string text) => text?.ToUpper() ?? "", 
            name: "to_uppercase", 
            description: "Converts text to uppercase");

        Console.WriteLine($"   Registered {registry.Tools.Count} tools:");
        foreach (var tool in registry.Tools)
        {
            Console.WriteLine($"     - {tool.Name}: {tool.Description}");
        }

        // STEP 2: Create an ACTUAL MCP server with ToolWire tools
        Console.WriteLine("\n2. Creating REAL MCP server with ToolWire tools...");
        
        var builder = Host.CreateApplicationBuilder();
        
        // Add MCP server with stdio transport - THIS IS REAL!
        builder.Services.AddMcpServer(options =>
        {
            options.ServerInfo = new Implementation
            {
                Name = "ToolWire-MCP-Server",
                Version = "1.0.0",
                Description = "An MCP server powered by ToolWire"
            };
            options.Capabilities = new ServerCapabilities
            {
                Tools = new ToolsCapability()
            };
        })
        .WithStdioServerTransport();

        // Create executor and register as singleton
        var executor = new ToolExecutor(registry);
        builder.Services.AddSingleton(executor);
        builder.Services.AddSingleton<IToolRegistry>(registry);

        // Register each ToolWire tool as an MCP server tool
        foreach (var tool in registry.Tools)
        {
            var mcpTool = new ToolWireMcpServerTool(tool, executor);
            builder.Services.AddSingleton<McpServerTool>(mcpTool);
        }

        Console.WriteLine($"   Added {registry.Tools.Count} ToolWireMcpServerTool instances to DI container");
        Console.WriteLine("   MCP server is configured with stdio transport");
        Console.WriteLine("   Server will listen for MCP protocol messages on stdin/stdout");

        // STEP 3: Show what the server will do
        Console.WriteLine("\n3. Server capabilities:");
        Console.WriteLine("   - Protocol: Model Context Protocol");
        Console.WriteLine("   - Transport: stdio (standard input/output)");
        Console.WriteLine("   - Tools: 6 ToolWire-powered tools");
        Console.WriteLine("   - Clients: Any MCP-compliant client can connect");

        // STEP 4: Convert ToolWire tools to MCP format for display
        Console.WriteLine("\n4. ToolWire tools converted to MCP protocol format:");
        var mcpTools = registry.ToMcpTools();
        foreach (var tool in mcpTools)
        {
            Console.WriteLine($"   Tool: {tool.Name}");
            Console.WriteLine($"     Description: {tool.Description}");
            Console.WriteLine($"     Input Schema: {tool.InputSchema}");
        }

        // STEP 5: Test ToolWire execution directly (simulating what MCP would call)
        Console.WriteLine("\n5. Testing ToolWire tool execution (same code MCP server would use):");
        await TestToolExecution(executor);

        // STEP 6: Show how to run the real server
        Console.WriteLine("\n6. To run the actual MCP server:");
        Console.WriteLine("   Uncomment the host.RunAsync() line below");
        Console.WriteLine("   Then connect any MCP client to this process");
        
        // Uncomment this to actually run the MCP server:
        // var host = builder.Build();
        // await host.RunAsync();
        
        Console.WriteLine("\n   (Server not started for this demo - showing setup only)");

        // Show the value proposition
        Console.WriteLine("\n=== Why ToolWire + REAL MCP? ===");
        Console.WriteLine("""
            
            **ToolWire** (C# Developer Experience):
            ✓ Register tools with simple lambdas
            ✓ Automatic JSON schema generation
            ✓ Type-safe parameter binding
            ✓ No ceremony - just write normal C#
            
            **ModelContextProtocol SDK** (Real Protocol):
            ✓ Official Microsoft SDK
            ✓ Full MCP protocol implementation
            ✓ Stdio, HTTP, SSE transports
            ✓ Tool discovery and invocation
            
            **ToolWire.Providers.Mcp** (The Bridge):
            ✓ ToolWireMcpServerTool wraps ToolWire tools
            ✓ Converts ToolWire execution to MCP format
            ✓ Seamless integration via DI
            ✓ Zero boilerplate connection code
            
            **Real Usage:**
            var registry = new ToolRegistry();
            registry.Register((string city) => GetWeather(city));
            
            builder.Services.AddMcpServer()
                .WithStdioServerTransport();
            
            // Register ToolWire tools with MCP
            foreach (var tool in registry.Tools)
            {
                var mcpTool = new ToolWireMcpServerTool(tool, executor);
                builder.Services.AddSingleton<McpServerTool>(mcpTool);
            }
            
            await host.RunAsync(); // MCP server running with ToolWire tools!
            """);
    }

    private static async Task TestToolExecution(ToolExecutor executor)
    {
        var tests = new (string toolName, object args)[]
        {
            ("add", new { a = 10, b = 20 }),
            ("multiply", new { a = 5, b = 6 }),
            ("get_weather", new { city = "Tokyo" })
        };

        foreach (var test in tests)
        {
            try
            {
                var jsonArgs = Newtonsoft.Json.JsonConvert.SerializeObject(test.args);
                var call = new ToolCall(
                    id: Guid.NewGuid().ToString(),
                    name: test.toolName,
                    arguments: Newtonsoft.Json.Linq.JObject.Parse(jsonArgs)
                );

                var result = await executor.ExecuteAsync(call);
                Console.WriteLine($"   {test.toolName}({jsonArgs}) => {result.Output}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   {test.toolName}: ERROR - {ex.Message}");
            }
        }
    }
}

public class WeatherService
{
    private readonly Random _random = new Random();
    
    public string GetWeather(string city)
    {
        var temp = _random.Next(32, 95);
        var conditions = new[] { "sunny", "cloudy", "rainy", "partly cloudy" };
        var condition = conditions[_random.Next(conditions.Length)];
        return $"{temp}°F, {condition} in {city}";
    }
}
