# Granit.Mcp

MCP (Model Context Protocol) module for Granit. Discovers attribute-annotated tools, resources, and prompts from module assemblies, with output sanitization for GDPR compliance and OpenTelemetry diagnostics. Built on the official MCP C# SDK.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Mcp
```

## Defining tools

Tools are plain classes discovered from module assemblies by attribute. Annotate the
class and its methods, then the module registers them at startup:

```csharp
[McpServerToolType]
[McpExposed]
public sealed class WeatherTools
{
    [McpServerTool(Name = "get_forecast")]
    [Description("Returns the weather forecast for a city.")]
    public static string GetForecast(string city) => /* ... */;
}
```

The **default** discovery mode is `Explicit`: a class is exposed only when annotated with
BOTH `[McpServerToolType]` (SDK) and `[McpExposed]` (Granit). Set
`GranitMcpOptions.ToolDiscovery = McpToolDiscoveryMode.Auto` to relax this so every
`[McpServerToolType]` class is discovered without `[McpExposed]`.

## Configuration

Configure via the `Mcp` section in `appsettings.json`:

- `ServerName` (string, default `"Granit"`)
- `ServerVersion` (string, optional)
- `ToolDiscovery` (`Explicit` | `Auto`, default `Explicit`)
- `EnableTenantFiltering` (bool, default `true`)
- `MaxResponseSizeBytes` (int, default `51200`)

## Dependencies

- `Granit`
- `ModelContextProtocol`

## Documentation

See the [full documentation](https://granit-fx.dev).
