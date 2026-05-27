# Granit.Features.Wolverine

Wolverine binding for [`Granit.Features`](https://granit-fx.dev). A pipeline middleware enforces the
`[RequiresFeature("name")]` attribute on message types before the handler runs, throwing
`FeatureNotEnabledException` when a required feature is disabled for the current tenant/plan context.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Features.Wolverine
```

## Usage

Decorate the message and register the middleware in your Wolverine setup:

```csharp
using Granit.Features.Wolverine.Attributes;

[RequiresFeature("export.pdf")] // feature under your FeatureDefinitionProvider
public sealed record GenerateExportCommand(Guid Id);

opts.Policies.AddMiddleware<RequiresFeatureMiddleware>(
    chain => chain.MessageType.HasAttribute<RequiresFeatureAttribute>());
```

`GranitFeaturesWolverineModule` pulls in the core `GranitFeaturesModule` automatically.

The middleware is discovered by Wolverine convention and uses no Wolverine types, so the package
carries **no** `WolverineFx` reference — the host's Wolverine setup is sufficient.

## Dependencies

- `Granit.Features`

## Documentation

See the [full documentation](https://granit-fx.dev).
