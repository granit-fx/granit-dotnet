# Granit.Oidc.TokenManagement

OAuth 2.0 token lifecycle management for the Granit framework.

## Features

- **Token endpoint operations**: strongly typed token requests with automatic
  discovery document resolution
- **Token revocation**: best-effort revocation via RFC 7009
- **Client credentials caching**: distributed cache with per-client stampede
  prevention via `SemaphoreSlim`
- **DelegatingHandler**: automatic token acquisition, caching, and 401 retry
  for `IHttpClientFactory` named clients
- **DPoP support**: sender-constrained tokens with automatic nonce retry
  (RFC 9449)
- **Diagnostics**: OpenTelemetry metrics and `ActivitySource` for distributed
  tracing

## Quick start

```csharp
builder.Services.AddClientCredentialsHttpClient("MyApi", options =>
{
    options.Authority = "https://idp.example.com";
    options.ClientId = "my-client";
    options.ClientSecret = "secret";
    options.Scope = "api.read";
});

// Inject via IHttpClientFactory
app.MapGet("/data", async (IHttpClientFactory factory) =>
{
    var client = factory.CreateClient("MyApi");
    var response = await client.GetAsync("https://api.example.com/data");
    return Results.Ok(await response.Content.ReadAsStringAsync());
});
```

## Dependencies

- `Granit.Oidc` — protocol primitives (discovery, DPoP, client
  authentication)
- `Granit` — module system, activity source registry
- `Granit.Timing` — `IClock` abstraction
- `Microsoft.Extensions.Caching.Abstractions` — `IDistributedCache`
- `Microsoft.Extensions.Http` — `IHttpClientFactory`
