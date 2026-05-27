# Granit.Http.Features

ASP.NET Core binding for [`Granit.Features`](https://granit-fx.dev). A Minimal-API endpoint filter
that enforces a feature check before the handler runs and returns HTTP 403 (`FeatureNotEnabledException`,
`errorCode: "Features:NotEnabled"`) when the feature is disabled for the current tenant/plan context.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Http.Features
```

## Usage

Reference `GranitHttpFeaturesModule` — it pulls in the core `GranitFeaturesModule` automatically —
then guard endpoints:

```csharp
using Granit.Http.Features.AspNetCore;

app.MapPost("/consultations/start", StartAsync)
   .RequiresFeature(AcmeFeatures.VideoConsultation.Name);

// Or on a group:
RouteGroupBuilder export = app.MapGroup("/export")
   .RequiresFeature(AcmeFeatures.ExportPdf.Name);
```

## Dependencies

- `Granit.Features`

## Documentation

See the [full documentation](https://granit-fx.dev).
