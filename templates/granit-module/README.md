# Granit.MyModule

Granit module for MyModule.

Part of the [granit](https://github.com/granit-fx/granit-dotnet) framework.

## Structure

```text
Granit.MyModule/
  Diagnostics/
    MyModuleActivitySource.cs   # Distributed tracing ActivitySource (one per module)
    MyModuleMetrics.cs          # OpenTelemetry counters injected via IMeterFactory
  Extensions/
    MyModuleHostApplicationBuilderExtensions.cs   # AddGranitMyModule() registration
  GranitMyModuleModule.cs       # Module class — declares [DependsOn] and wires registration
```

## Wiring

`GranitMyModuleModule` is auto-discovered by the Granit module system. Declare direct
project dependencies with `[DependsOn]`:

```csharp
[DependsOn(typeof(GranitGuidsModule), typeof(GranitTimingModule))]
public sealed class GranitMyModuleModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitMyModule();
}
```

Register services, query/export definitions, and cross-cutting concerns inside
`AddGranitMyModule`. See `Granit.Invoicing` for a reference implementation.

## Installation

```bash
dotnet add package Granit.MyModule
```

## Documentation

See the [full documentation](https://github.com/granit-fx/granit-dotnet).
