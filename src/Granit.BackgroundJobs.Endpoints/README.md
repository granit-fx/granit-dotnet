# Granit.BackgroundJobs.Endpoints

Minimal API endpoints for administering Granit recurring background jobs. Exposes GET/POST routes for listing, pausing, resuming, and triggering jobs.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.BackgroundJobs.Endpoints
```

## Dependencies

- `Granit.Authorization`
- `Granit.BackgroundJobs`
- `Granit.QueryEngine`

## Usage

Declare the module on the host's root module so its services are registered:

```csharp
[DependsOn(typeof(GranitBackgroundJobsEndpointsModule))]
public sealed class MyAppModule : GranitModule { }
```

Then map the routes during endpoint registration (the package adds no endpoints
until this call):

```csharp
app.MapGranitBackgroundJobs();

// Or under a route group:
api.MapGranitBackgroundJobs();
```

This exposes 5 endpoints (GET list, GET by name, POST pause/resume/trigger)
under the default prefix `background-jobs`. Customise via the configure delegate
(`opts => opts.RoutePrefix = "admin/jobs"`) or bind from the
`BackgroundJobs:Endpoints` config section. Read endpoints require the
`BackgroundJobs.Jobs.Read` permission; write endpoints require
`BackgroundJobs.Jobs.Manage`.

## Documentation

See the [full documentation](https://granit-fx.dev).
