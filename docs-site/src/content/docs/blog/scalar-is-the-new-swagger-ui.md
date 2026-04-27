---
title: "Scalar Is the New Swagger UI — Here's How to Switch"
date: 2026-03-28
sidebar:
  order: 13
authors:
  - jfmeyers
tags:
  - tutorial
  - api
description: "Swashbuckle is unmaintained and has no OpenAPI 3.1 support. .NET 10 ships a native document generator. Here is how to replace Swagger UI with Scalar in minutes."
excerpt: >
  Swashbuckle is unmaintained and has no OpenAPI 3.1 support. .NET 10 ships a
  native document generator. Here is how to replace Swagger UI with Scalar — and
  what Granit's ApiDocumentation module does for you automatically.
---

Swashbuckle.AspNetCore is unmaintained. It does not support OpenAPI 3.1. It conflicts
with ASP.NET Core's native endpoint metadata pipeline. If you are still using it in a
.NET 10 project, you are carrying dead weight.

Microsoft shipped **native OpenAPI generation** starting with .NET 9
(`Microsoft.AspNetCore.OpenApi`). No reflection hacks, no custom `ISchemaFilter` chains,
no Swashbuckle. The generated document is OpenAPI **3.1**, not 2.0. **Scalar** is the
modern UI that consumes it.

This tutorial walks you through the switch — first the raw plumbing, then via Granit's
`Granit.Http.ApiDocumentation` module, which wires everything in two lines.

## Why drop Swashbuckle?

Three reasons that matter in production:

- **No OpenAPI 3.1** — Swashbuckle generates Swagger 2.0 / OpenAPI 3.0 only. Modern
  tooling (Scalar, Stoplight, Redocly), JSON Schema `const`/`$ref` siblings, and
  `webhooks` all require 3.1.
- **Abandoned maintenance** — the upstream maintainer archived the project. .NET version
  compatibility and security patches are community best-effort.
- **Conflicts with native endpoint metadata** — .NET 9+ Minimal APIs and
  `IEndpointMetadata` are designed for `Microsoft.AspNetCore.OpenApi`. Swashbuckle plugs
  in at a different layer and silently misses metadata registered with `.Produces<T>()`
  and `.ProducesProblem()`.

Microsoft removed Swashbuckle from the default `webapi` template in .NET 9 for exactly
these reasons.

## What .NET 10 gives you natively

`Microsoft.AspNetCore.OpenApi` hooks directly into the ASP.NET Core endpoint data source.
The document is generated from the metadata you already declared — no XML comment parsing,
no reflection scans.

```csharp title="Program.cs — raw .NET 10 setup"
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi(); // Native OpenAPI generation

var app = builder.Build();
app.MapOpenApi("/openapi/v1.json"); // Serves the JSON document
await app.RunAsync();
```

That is the entire document pipeline. Navigate to `/openapi/v1.json` in Development and
you get a valid OpenAPI 3.1 document reflecting every endpoint you registered.

## Scalar: the UI that replaced Swagger UI

**Scalar** is an open-source API reference portal and interactive client. It reads an
OpenAPI 3.1 document and provides:

- A searchable reference UI (dark/light mode, request examples, code generation)
- An **interactive request builder** — test endpoints directly in the browser
- Built-in **OAuth2 Authorization Code + PKCE** authentication
- Code snippets in 15+ languages per operation

Adding it takes one package (`Scalar.AspNetCore`) and two lines:

```csharp title="Program.cs — adding Scalar"
using Scalar.AspNetCore;

app.MapOpenApi("/openapi/v1.json");
app.MapScalarApiReference(); // Serves Scalar UI at /scalar
```

Navigate to `/scalar`. You get a fully interactive API explorer backed by your live
OpenAPI document.

## The production plumbing you would write manually

A real setup adds security schemes, authorization, and branding:

```csharp title="Program.cs — manual production setup"
// Service registration
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((doc, _, _) =>
    {
        doc.Info = new OpenApiInfo { Title = "My API", Version = "1.0" };
        return Task.CompletedTask;
    });
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

// Middleware pipeline
app.MapOpenApi("/openapi/v1.json")
   .RequireAuthorization("InternalDeveloper"); // Protect in production

app.MapScalarApiReference(opts =>
{
    opts.WithTitle("My API");
    opts.AddAuthorizationCodeFlow("OAuth2", flow =>
        flow.WithClientId("my-frontend-client")
            .WithPkce(Pkce.Sha256));
});
```

You write this in every project. Then you forget the transformer in one. Then you
accidentally leave the endpoint unprotected in staging.

## The Granit approach

`Granit.Http.ApiDocumentation` wraps the entire setup behind a module declaration, one
middleware call, and a config section. All transformers are auto-registered. Production
protection is opt-in. You cannot forget.

### Step 1 — Declare the module

```csharp title="AppHostModule.cs"
[DependsOn(typeof(GranitHttpApiDocumentationModule))]
public sealed class AppHostModule : GranitModule { }
```

`GranitHttpApiDocumentationModule` calls `AddGranitApiDocumentation()` internally. This
registers all document transformers:

- JWT Bearer and OAuth2 security schemes
- `ProblemDetails` schema (RFC 7807 error responses)
- Internal type filtering (hides framework-internal types from the public document)
- Tenant header injection (optional, for multi-tenant APIs)
- FluentValidation schema enrichment (constraint annotations: `maxLength`, `pattern`, etc.)

### Step 2 — Expose the endpoints

```csharp title="Program.cs"
await app.UseGranitAsync();
app.UseGranitApiDocumentation(); // Maps /openapi/v{n}.json and Scalar UI
```

`UseGranitApiDocumentation()` is a **no-op in Production** unless
`EnableInProduction: true` is set. You cannot accidentally expose your schema in a
production environment.

### Step 3 — Configure via appsettings

```json title="appsettings.json"
{
  "ApiDocumentation": {
    "Title": "Guava API",
    "Description": "Internal REST API for the Guava platform.",
    "PartyEmail": "api-support@example.com",
    "LogoUrl": "/logo.svg",
    "FaviconUrl": "/favicon.svg",
    "MajorVersions": [1, 2],
    "EnableInProduction": false,
    "AuthorizationPolicy": "InternalDeveloper"
  }
}
```

`MajorVersions: [1, 2]` generates two independent documents — `/openapi/v1.json` and
`/openapi/v2.json` — with a single Scalar UI that switches between them.

## OAuth2 PKCE in the browser

If your API is protected by an OAuth2 provider (Keycloak, Entra, Auth0), Scalar can
authenticate on behalf of the developer without leaving the browser. Add the `OAuth2`
sub-section:

```json title="appsettings.json — OAuth2 section"
{
  "ApiDocumentation": {
    "OAuth2": {
      "AuthorizationUrl": "https://auth.example.com/realms/my-realm/protocol/openid-connect/auth",
      "TokenUrl": "https://auth.example.com/realms/my-realm/protocol/openid-connect/token",
      "ClientId": "my-frontend-client",
      "EnablePkce": true,
      "Scopes": ["openid", "profile"]
    }
  }
}
```

Scalar shows a **Sign in** button. The Authorization Code + PKCE flow runs in the
browser. The token is automatically attached to every test request. No Postman
collection, no manual Bearer header, no copy-pasting tokens.

> Use a **public** (frontend) client — not your backend confidential client. Never put
> a `ClientSecret` here.

## Controlling access to the docs endpoint

The `AuthorizationPolicy` option determines who can reach `/openapi/v{n}.json` and the
Scalar UI:

| Value | Behavior |
|-------|----------|
| `null` (default) | Inherits the application's global authorization policy |
| `""` (empty string) | Explicit `AllowAnonymous` — public documentation |
| `"InternalDeveloper"` | Requires that named authorization policy |

The typical pattern: no policy in Development (inherits `AllowAnonymous` from `app.UseGranitAsync()`), named policy in Staging, `EnableInProduction: false` in Production.

## Key takeaways

- **Swashbuckle does not support OpenAPI 3.1** and is unmaintained — stop using it on
  .NET 9+.
- **`Microsoft.AspNetCore.OpenApi`** is the first-party document generator built into
  ASP.NET Core. No extra packages required.
- **Scalar** replaces Swagger UI with a modern UX, OAuth2 PKCE authentication, and
  per-operation code generation.
- **`Granit.Http.ApiDocumentation`** wires everything in one module declaration and one
  middleware call. All transformers, multi-version support, and production guards are
  included.
- **`EnableInProduction: false`** is the default — you cannot accidentally expose your
  schema in production.

## Further reading

- [API Documentation reference](/dotnet/api/api-documentation/) — full option reference,
  transformer pipeline, custom schema examples
- [ADR-009: Scalar API Documentation](/dotnet/architecture/adr/009-scalar-api-documentation/) — decision record and alternatives considered
- [API versioning with Asp.Versioning](/dotnet/api/api-versioning/) — combining multi-version
  docs with `MapApiGroup()`
- [Never Return Your EF Entities From an API](/blog/never-return-ef-entities/) — keeping
  your OpenAPI schema clean with response records
