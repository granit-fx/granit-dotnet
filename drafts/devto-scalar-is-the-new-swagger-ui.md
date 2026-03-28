---
title: Swashbuckle Is Dead. Here's How to Migrate to Scalar in .NET 10.
published: false
description: >
  Swashbuckle is abandoned and doesn't support OpenAPI 3.1. .NET 10 ships
  a native document generator. Here is the complete migration guide to Scalar —
  including security schemes, OAuth2 PKCE, multi-version APIs, and common gotchas.
tags: dotnet, csharp, api, webdev
cover_image:
canonical_url:
---

I upgraded a project to .NET 9 last year and the CI started failing.
The culprit: Swashbuckle. It had a hard dependency on a Swagger UI version
that was incompatible with a transitive package update. I went to file an issue
and found the repository archived.

That was the push I needed. I had been meaning to move to Scalar for a while.
Here is what I learned from migrating several projects.

---

## Why Swashbuckle is no longer the right choice

**Swashbuckle.AspNetCore** was the de facto Swagger UI for .NET for years.
Today it has three problems that are hard to work around:

| Problem | Impact |
|---------|--------|
| No OpenAPI 3.1 support | Can't use JSON Schema features (`const`, webhooks, `$ref` siblings) |
| Upstream archived | No security patches, no .NET compatibility updates |
| Bypasses native metadata | Misses `.Produces<T>()`, `.ProducesProblem()` on Minimal APIs |

Microsoft removed Swashbuckle from the `dotnet new webapi` template in .NET 9 and
replaced it with `Microsoft.AspNetCore.OpenApi`. That was the official signal.

---

## What replaced it

Two components, one stack:

- **`Microsoft.AspNetCore.OpenApi`** — first-party document generator, ships with
  .NET 9+. Hooks directly into the ASP.NET Core endpoint data source. No XML comment
  parsing, no reflection gymnastics. Generates **OpenAPI 3.1** from the metadata you
  already declared.

- **`Scalar.AspNetCore`** — open-source API reference portal (MIT). Reads any
  OpenAPI 3.1 document and renders an interactive UI with OAuth2/PKCE auth, code
  generation in 15+ languages, and a clean dark/light mode design.

They are independent. You can use the native generator with a different UI, or Scalar
with a document from NSwag. In practice, using them together is the obvious choice.

---

## The migration — step by step

### 1. Remove Swashbuckle

```bash
dotnet remove package Swashbuckle.AspNetCore
```

Also remove any `Swashbuckle.AspNetCore.*` packages (annotations, filters, etc.).
Delete the `AddSwaggerGen` and `UseSwagger` / `UseSwaggerUI` calls from `Program.cs`.

### 2. Add the new packages

```bash
dotnet add package Microsoft.AspNetCore.OpenApi
dotnet add package Scalar.AspNetCore
```

### 3. Register document generation

```csharp title="Program.cs"
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Native OpenAPI document generation
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((doc, _, _) =>
    {
        doc.Info = new OpenApiInfo
        {
            Title = "Bookings API",
            Version = "1.0",
            Description = "Hotel booking management API.",
        };
        return Task.CompletedTask;
    });
});

var app = builder.Build();
```

### 4. Map the endpoints

```csharp title="Program.cs (continued)"
if (app.Environment.IsDevelopment())
{
    // Serves /openapi/v1.json
    app.MapOpenApi("/openapi/v1.json");

    // Serves Scalar UI at /scalar
    app.MapScalarApiReference();
}

await app.RunAsync();
```

That is the minimum viable setup. Navigate to `/scalar` and you have an interactive
API explorer backed by your live OpenAPI 3.1 document.

---

## Adding JWT Bearer authentication

Swashbuckle had `AddSecurityDefinition` / `AddSecurityRequirement`. The native pipeline
uses **document transformers**.

```csharp title="BearerSecuritySchemeTransformer.cs"
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

internal sealed class BearerSecuritySchemeTransformer(
    IAuthenticationSchemeProvider authenticationSchemeProvider)
    : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        IEnumerable<AuthenticationScheme> schemes =
            await authenticationSchemeProvider.GetAllSchemesAsync();

        if (!schemes.Any(s => s.Name == "Bearer"))
        {
            return;
        }

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, OpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter your JWT token.",
        };
    }
}
```

Register it before `AddOpenApi`:

```csharp
builder.Services.AddTransient<BearerSecuritySchemeTransformer>();

builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    // ...
});
```

> **Note**: the transformer must be registered in DI *before* `AddOpenApi` is called,
> or the DI resolution will fail at startup.

---

## OAuth2 Authorization Code + PKCE

If your API is protected by an OAuth2 provider (Keycloak, Entra ID, Auth0), you can
configure Scalar to authenticate directly in the browser — no Postman needed.

```csharp title="Program.cs"
app.MapScalarApiReference(opts =>
{
    opts.WithTitle("Bookings API");
    opts.AddAuthorizationCodeFlow("OAuth2", flow =>
    {
        flow.WithClientId("bookings-frontend-client")
            .WithSelectedScopes("openid", "profile")
            .WithPkce(Pkce.Sha256);
    });
});
```

Scalar shows a **Sign in** button. The flow runs entirely in the browser. The resulting
token is automatically attached to every test request.

**Use a public (frontend) client.** Never configure your backend confidential client
here — it would expose the client secret in a browser context.

---

## Multi-version APIs

One document per major version. Each call to `AddOpenApi` registers an independent
document:

```csharp
builder.Services.AddOpenApi("v1", options => { /* ... */ });
builder.Services.AddOpenApi("v2", options => { /* ... */ });

app.MapOpenApi("/openapi/v1.json");
app.MapOpenApi("/openapi/v2.json");

app.MapScalarApiReference(opts =>
{
    opts.WithEndpointPrefix("/scalar/{documentName}");
});
```

The default Scalar route (`/scalar/{documentName}`) handles version switching in the UI.

---

## Common gotchas

### `.Produces<T>()` is required — there is no fallback inference

Swashbuckle could sometimes infer response types by inspecting the action return type.
The native pipeline does not. If you omit `.Produces<T>()`, the operation has no
response schema.

```csharp
// Missing .Produces<BookingResponse>() → no response type in the OpenAPI document
app.MapGet("/bookings/{id:guid}", GetBookingAsync)
    .WithName("GetBooking")
    .WithSummary("Returns a booking by ID.")
    .ProducesProblem(StatusCodes.Status404NotFound);
    // .Produces<BookingResponse>() ← you must add this

// Correct
app.MapGet("/bookings/{id:guid}", GetBookingAsync)
    .WithName("GetBooking")
    .WithSummary("Returns a booking by ID.")
    .Produces<BookingResponse>()
    .ProducesProblem(StatusCodes.Status404NotFound);
```

### Use `TypedResults`, not `Results`

`Results.Ok(value)` returns `IResult` at compile time — the native pipeline cannot
determine the response type. `TypedResults.Ok(value)` returns `Ok<T>` — the type is
known statically and shows up in the OpenAPI schema automatically.

```csharp
// BAD — type lost at compile time
return Results.Ok(booking);

// GOOD — type preserved, OpenAPI schema complete
return TypedResults.Ok(booking);
```

### Protect the docs endpoint in non-development environments

`app.MapOpenApi()` and `app.MapScalarApiReference()` create unauthenticated endpoints
by default. In staging or production, protect them explicitly:

```csharp
app.MapOpenApi("/openapi/v1.json")
   .RequireAuthorization("InternalDeveloper");

app.MapScalarApiReference()
   .RequireAuthorization("InternalDeveloper");
```

Or gate the entire block with `IsDevelopment()` and add explicit staging config.

### Transformers run in registration order

Document transformers execute in the order they are registered. If transformer B
depends on data set by transformer A, register A first. This tripped me up with a
security scheme transformer that assumed `doc.Components` was already initialized.

---

## Scalar vs Swagger UI — feature comparison

| Feature | Swagger UI (Swashbuckle) | Scalar |
|---------|--------------------------|--------|
| OpenAPI version | 2.0 / 3.0 | 3.1 |
| .NET 9/10 native pipeline | No | Yes |
| Interactive request builder | Yes | Yes |
| OAuth2 PKCE in browser | Partial | Yes, first-class |
| Code generation | No | 15+ languages |
| Dark mode | Third-party themes | Built-in |
| Maintenance status | Archived | Active (MIT) |

---

## If you use a framework that already handles this

Rolling the transformer pipeline manually gets repetitive across projects. If you work
with the open-source **[Granit](https://granit-fx.dev)** framework for .NET, all of
this is pre-wired in the `Granit.Http.ApiDocumentation` module: security schemes,
OAuth2 PKCE, multi-version documents, and production protection are all configured
from a single `appsettings.json` section.

```json
{
  "ApiDocumentation": {
    "Title": "Bookings API",
    "MajorVersions": [1, 2],
    "AuthorizationPolicy": "InternalDeveloper",
    "OAuth2": {
      "AuthorizationUrl": "https://auth.example.com/.../auth",
      "TokenUrl": "https://auth.example.com/.../token",
      "ClientId": "bookings-frontend-client"
    }
  }
}
```

Worth a look if you are building a new modular .NET backend from scratch.

---

## Summary

The migration from Swashbuckle to Scalar on .NET 10 is straightforward once you know
the transformer pattern. The short version:

1. Remove `Swashbuckle.AspNetCore`, add `Microsoft.AspNetCore.OpenApi` +
   `Scalar.AspNetCore`
2. Replace `AddSwaggerGen` with `AddOpenApi` + document transformer for metadata
3. Replace `UseSwagger` / `UseSwaggerUI` with `MapOpenApi` + `MapScalarApiReference`
4. Switch all handlers from `Results.*` to `TypedResults.*`
5. Add `.Produces<T>()` explicitly on every endpoint
6. Protect the docs endpoints outside of development

The result: OpenAPI 3.1, a modern interactive UI, native OAuth2 PKCE, and no more
dependency on an archived package.

---

*Have questions or migration tips to share? Drop them in the comments.*
