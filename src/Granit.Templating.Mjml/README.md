# Granit.Templating.Mjml

MJML email template support for the Granit framework. Compiles MJML markup to
email-client-safe HTML (table-based layout, inline CSS, MSO conditional comments)
using [Mjml.Net](https://github.com/SebastianStehle/mjml-net) — a native .NET MJML
compiler with 100% feature parity. Zero Node.js dependency.

## How it works

`MjmlTransformer` implements `IRenderedContentTransformer` (Order=100) and runs in the
post-render pipeline. It auto-detects MJML content by checking if the rendered output
starts with `<mjml>`. Plain HTML templates pass through unchanged.

## Setup

```csharp
services.AddGranitTemplatingWithScriban();
services.AddGranitTemplatingWithMjml();
```

Or with the module system:

```csharp
[DependsOn(
    typeof(GranitTemplatingScribanModule),
    typeof(GranitTemplatingMjmlModule))]
public class AppModule : GranitModule { }
```

## Documentation

See the [MJML Email Templates](https://granit.dev/dotnet/business/templating/mjml-email-templates/)
guide for full usage examples.
