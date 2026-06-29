# Granit.Mcp.Server

ASP.NET Core MCP server integration for Granit. Streamable HTTP transport via MapGranitMcpServer(), authorization via [Authorize] + AddAuthorizationFilters(), RBAC permissions, multi-tenant tool visibility, and module scoping.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Mcp.Server
```

## Integration

Three steps wire the MCP HTTP transport into a Granit host:

1. Declare the module dependency on your host `GranitModule` so MCP services register:

   ```csharp
   [DependsOn(typeof(GranitMcpServerModule))]
   public sealed class YourHostModule : GranitModule;
   ```

2. Import the extensions namespace in `Program.cs`:

   ```csharp
   using Granit.Mcp.Server.Extensions;
   ```

3. Map the endpoint on the route builder (optionally with a `GranitMcpServerOptions`
   configurator). It can sit outside the versioned API group as a cross-cutting agent
   surface:

   ```csharp
   app.MapGranitMcpServer();
   ```

## Dependencies

- `Granit.Mcp`
- `Granit.Authorization`
- `ModelContextProtocol.AspNetCore`

## Documentation

See the [full documentation](https://granit-fx.dev).
