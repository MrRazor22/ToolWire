# ToolWire

[![NuGet](https://img.shields.io/nuget/v/ToolWire.svg)](https://www.nuget.org/packages/ToolWire)
[![Build Status](https://github.com/yourusername/toolwire/workflows/Build%20&%20Test/badge.svg)](https://github.com/yourusername/toolwire/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**Lightweight, composable tool-calling infrastructure for .NET AI agents.**

ToolWire is the "tool calling infrastructure layer" that every AI agent framework needs but doesn't need to own. It handles the messy parts of exposing .NET methods as LLM-callable tools—automatic JSON schema generation, type-safe parameter binding, and reliable execution—without imposing any architectural decisions on your application.

## Why ToolWire?

Most .NET AI frameworks (Microsoft Agent Framework, AutoGen .NET, Semantic Kernel) are **heavyweight, opinionated stacks** that dictate:
- How you structure agents
- Which LLM providers to use
- How orchestration works
- Your entire agent lifecycle

**ToolWire is different.** It's a focused, composable layer that handles **just the tool execution plumbing**:

```csharp
// Register any method as an LLM tool
[Tool]
[Description("Calculate the sum of two numbers")]
public double Add(double a, double b) => a + b;

// That's it. ToolWire handles schema generation, validation, and execution.
```

### Perfect For:
- Enterprise teams with existing LLM integrations who need **solid primitives**
- Developers building custom agent frameworks who want **proven tool infrastructure**
- Projects that need to integrate AI tools into **legacy systems**
- Anyone who wants **framework flexibility** without vendor lock-in

## Quick Start

### Installation

```bash
dotnet add package ToolWire
```

### Basic Usage

```csharp
using ToolWire.Tools;

// 1. Create a registry to hold your tools
var registry = new ToolRegistry();

// 2. Register methods as tools
registry.Register((string name) => $"Hello, {name}!", 
    name: "greet", 
    description: "Greets a person by name");

registry.Register((double a, double b) => a + b, 
    name: "add", 
    description: "Adds two numbers");

// 3. Create an executor
var executor = new ToolExecutor(registry);

// 4. Execute tool calls from your LLM
var toolCall = new ToolCall(
    id: "call-1",
    name: "add",
    arguments: new JObject { ["a"] = 5, ["b"] = 3 }
);

var result = await executor.ExecuteAsync(toolCall);
Console.WriteLine(result.Output); // "8"
```

### Using Attributes for Cleaner Code

```csharp
public class CalculatorTools
{
    [Tool]
    [Description("Adds two numbers")]
    public double Add(double a, double b) => a + b;

    [Tool]
    [Description("Multiplies two numbers")]
    public double Multiply(double a, double b) => a * b;

    [Tool]
    [Description("Gets the current temperature for a city")]
    public async Task<string> GetWeather(
        [Description("City name, e.g., 'New York'")] string city,
        CancellationToken ct = default)
    {
        // Your implementation here
        return "72°F, sunny";
    }
}

// Register all attributed methods at once
var registry = new ToolRegistry();
registry.RegisterAll<CalculatorTools>();
```

## Features

### Automatic JSON Schema Generation

ToolWire automatically generates valid JSON schemas for your tools from .NET method signatures:

```csharp
public record Person(
    [Description("Full name")] string Name,
    [Range(0, 120)] int Age,
    [EmailAddress] string Email,
    [Required] string UserId
);

[Tool]
public string ProcessPerson(Person person) => $"Processing {person.Name}";
```

Generates the complete JSON schema with:
- Property descriptions
- Type mappings (string, integer, number, boolean, array, object)
- Enum support
- Nullable handling
- Validation attributes (Range, StringLength, EmailAddress, Required)
- Nested object support

### Type-Safe Parameter Binding

ToolWire automatically binds LLM-provided JSON arguments to method parameters:

```csharp
// Async support
[Tool]
public async Task<string> FetchData(string url, CancellationToken ct);

// Default values
[Tool]
public string Repeat(string text, int count = 3);

// Nullable parameters
[Tool]
public string Optional(string? prefix, string content);

// Complex objects
[Tool]
public decimal CalculateTotal(Order order);

// Enums
[Tool]
public string GetDayName(DayOfWeek day);
```

### Provider Integrations

ToolWire works with any LLM provider. We provide convenience packages for popular ones:

#### OpenAI

```bash
dotnet add package ToolWire.Providers.OpenAI
```

```csharp
using ToolWire.Providers.OpenAI;
using OpenAI.Responses;

var executor = new ToolExecutor(registry);

// In your agent loop
var response = await client.CreateResponseAsync(request);
foreach (var toolCall in response.OutputItems.OfType<FunctionCallResponseItem>())
{
    var result = await executor.ExecuteAsync(toolCall);
    conversation.Add(result);
}
```

## Advanced Usage

### Timeout Handling

```csharp
// Set a timeout for all tool executions
var executor = new ToolExecutor(registry, TimeSpan.FromSeconds(30));

// Handle timeouts gracefully
var result = await executor.ExecuteAsync(toolCall);
if (result.IsError && result.Output.Contains("timed out"))
{
    // Handle timeout
}
```

### Event Hooks

```csharp
var executor = new ToolExecutor(registry);

// Log all tool invocations
executor.OnInvoking += (call) => 
    logger.LogInformation("Executing {ToolName}", call.Name);

// Handle errors
executor.OnError += (call, ex) => 
    logger.LogError(ex, "Tool {ToolName} failed", call.Name);

// Track completions
executor.OnCompleted += (call, result) => 
    metrics.RecordToolExecution(call.Name, result.IsError);
```

### Custom Validation

ToolWire validates parameters against the generated JSON schema before execution:

```csharp
// This will fail before your method is called
var toolCall = new ToolCall("add", new JObject { ["a"] = "not a number", ["b"] = 3 });
var result = await executor.ExecuteAsync(toolCall);
// result.IsError == true
// result.Output contains validation error details
```

### Tool Call Extraction from LLM Output

For LLMs that don't natively support function calling:

```csharp
var content = await llm.GenerateAsync(prompt);

// Extract tool calls from free-form text
var (prefix, toolCall) = content.TryExtractToolCall(registry);
if (toolCall != null)
{
    var result = await executor.ExecuteAsync(toolCall);
    // Continue conversation with result
}
```

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      Your Agent Logic                        │
│          (Orchestration, State, LLM Integration)            │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                      ToolWire Layer                          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │   Registry   │  │   Executor   │  │   Schema     │      │
│  │  (Discovery) │  │ (Invocation) │  │ (Generation) │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                    Your Business Logic                       │
│              (Methods marked with [Tool])                    │
└─────────────────────────────────────────────────────────────┘
```

ToolWire sits between your agent logic and your business logic, handling:
- **Discovery**: Finding methods marked with `[Tool]`
- **Schema Generation**: Converting .NET types to JSON schemas
- **Invocation**: Safely executing tools with parameter binding
- **Validation**: Ensuring LLM-provided arguments match expected types

## API Reference

### Core Classes

- `ToolRegistry`: Discovers and stores tool definitions
- `ToolExecutor`: Executes tool calls safely with timeout and error handling
- `Tool`: Represents a registered tool with schema and executor
- `ToolCall`: Represents a tool invocation from an LLM
- `ToolResult`: Represents the output of a tool execution

### Attributes

- `[Tool]`: Marks a method as an LLM-exposed tool
- `[Description]`: Provides descriptions for tools and parameters
- Standard .NET validation attributes: `[Range]`, `[StringLength]`, `[EmailAddress]`, `[Required]`

## Comparison with Alternatives

| Feature | ToolWire | Microsoft Agent Framework | AutoGen .NET | Semantic Kernel |
|---------|----------|---------------------------|--------------|-----------------|
| **Scope** | Tool infrastructure only | Full agent framework | Full agent framework | Full AI SDK |
| **Framework Lock-in** | None | High | High | High |
| **Learning Curve** | Low | High | Medium | High |
| **Custom Agent Logic** | Full control | Limited | Limited | Limited |
| **LLM Provider** | Any | Azure/OpenAI | Azure/OpenAI | Azure/OpenAI |
| **Size** | Lightweight | Heavyweight | Medium | Heavyweight |
| **Best For** | Custom frameworks | MS ecosystem | Research/prototyping | Enterprise Azure |

## Requirements

- .NET 8.0 or .NET 10.0
- Newtonsoft.Json 13.0.4+

## Contributing

We welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for details.

## License

MIT License - see [LICENSE](LICENSE) for details.

## Roadmap

- [x] Core tool registration and execution
- [x] Automatic JSON schema generation
- [x] OpenAI provider integration
- [ ] Anthropic provider integration
- [ ] Gemini provider integration
- [ ] Streaming tool execution support
- [ ] Tool composition and chaining
- [ ] Built-in common tools library

---

**ToolWire**: The boring infrastructure your exciting AI agents need. 🔧
