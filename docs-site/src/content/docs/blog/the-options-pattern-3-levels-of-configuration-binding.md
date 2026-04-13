---
title: "The Options Pattern — 3 Levels of Configuration Binding"
date: 2026-04-15
sidebar:
  order: 21
authors:
  - jfmeyers
tags:
  - best-practice
  - architecture
  - tutorial
description: "IOptions, IOptionsSnapshot, and IOptionsMonitor solve different problems. Picking the wrong one causes stale config, unnecessary allocations, or subtle concurrency bugs."
excerpt: >
  IOptions, IOptionsSnapshot, and IOptionsMonitor solve different problems. Picking
  the wrong one causes stale config, unnecessary allocations, or subtle concurrency
  bugs. Here is when to use each — with real examples from Granit's 128 packages.
---

You bind your configuration to a POCO, inject `IOptions<T>`, and move on. It works. Until it does not — your feature flag change requires a pod restart, your background worker reads stale config for hours, or your per-request options snapshot allocates a new object 5,000 times per second for no reason.

The .NET Options pattern has **three levels**. Each one exists for a reason, and using the wrong one creates problems that are invisible until production.

## Level 1: `IOptions<T>` — read once, cached forever

`IOptions<T>` resolves the configuration **once** at first injection and caches the result for the lifetime of the application. It is a singleton. It never re-reads the configuration source.

```csharp title="WebhooksOptions.cs — registration"
builder.Services
    .AddOptions<WebhooksOptions>()
    .BindConfiguration("Webhooks")
    .ValidateOnStart();
```

```csharp title="WebhookDispatcher.cs — consumption"
public sealed class WebhookDispatcher(IOptions<WebhooksOptions> options)
{
    private readonly WebhooksOptions _options = options.Value;

    public async Task DispatchAsync(WebhookEvent webhookEvent,
        CancellationToken cancellationToken = default)
    {
        if (_options.MaxRetryAttempts <= 0) return;
        // _options never changes — same instance for the entire app lifetime
    }
}
```

### When to use it

- **Static configuration** that does not change without a restart: database connection strings, feature module toggles, API base URLs, encryption key names.
- **Singleton services** — `IOptionsSnapshot<T>` and `IOptionsMonitor<T>` are scoped and singleton respectively, but `IOptions<T>` is the simplest choice when you know the value is fixed.
- **Startup validation** — combine with `ValidateOnStart()` to fail fast if configuration is invalid. The application does not start with a missing connection string.

### When NOT to use it

- Configuration that changes at runtime (feature flags, rate limits, cleanup intervals)
- Named options (multiple instances of the same type with different names)

### The trap

`IOptions<T>` does **not** support named options. Calling `IOptions<T>.Value` always returns the default (unnamed) instance. If you register named options with `services.Configure("client-a", ...)`, `IOptions<T>` ignores them silently.

## Level 2: `IOptionsSnapshot<T>` — fresh per request

`IOptionsSnapshot<T>` is **scoped**. It re-reads the configuration at the beginning of each DI scope — typically once per HTTP request. Within the scope, the value is consistent. Across scopes, it picks up changes.

```csharp title="FeatureFlagMiddleware.cs"
public sealed class FeatureFlagMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context,
        IOptionsSnapshot<FeatureOptions> options)
    {
        // options.Value is fresh for this request
        if (options.Value.EnableBetaEndpoints)
        {
            context.Items["beta"] = true;
        }

        await next(context).ConfigureAwait(false);
    }
}
```

### When to use it

- **Configuration that may change between requests** without a restart: feature flags backed by a config provider that supports reload, rate limit thresholds, UI toggle switches.
- **Per-request consistency** is required — all code within the same request sees the same configuration snapshot.
- **Named options** — `IOptionsSnapshot<T>` supports `.Get(name)` for named instances.

### When NOT to use it

- **Singleton services** — you cannot inject `IOptionsSnapshot<T>` into a singleton. The DI container will throw at runtime because a scoped service cannot be consumed by a singleton.
- **Background services** (`BackgroundService`, `IHostedService`) — these are singletons. Use `IOptionsMonitor<T>` instead.

### The trap

`IOptionsSnapshot<T>` allocates a **new options instance per scope**. If your configuration never actually changes, you are paying the allocation cost for nothing. At 5,000 requests per second, that is 5,000 objects per second that `IOptions<T>` would have served from a single cached instance.

## Level 3: `IOptionsMonitor<T>` — live reload with change notification

`IOptionsMonitor<T>` is a **singleton** that actively watches the configuration source for changes. When a change is detected, it updates its internal cache and fires an `OnChange` callback. You always get the latest value via `.CurrentValue`.

```csharp title="AuditingCleanupWorker.cs — background service"
internal sealed partial class AuditingCleanupWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<AuditingOptions> optionsMonitor,
    IClock clock,
    AuditingMetrics metrics,
    ILogger<AuditingCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PurgeExpiredEntriesAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogCleanupFailed(ex);
            }

            // Re-reads interval on every iteration — no restart needed
            await Task.Delay(optionsMonitor.CurrentValue.CleanupInterval, stoppingToken)
                .ConfigureAwait(false);
        }
    }
}
```

Every loop iteration reads `optionsMonitor.CurrentValue.CleanupInterval`. If an operator changes the interval from 5 minutes to 1 minute via a config reload, the next iteration picks it up — no pod restart, no deployment.

### Named options with `IOptionsMonitor<T>.Get(name)`

`IOptionsMonitor<T>` supports named options, which is critical for patterns like per-client HTTP configuration:

```csharp title="ClientCredentialsTokenHandler.cs"
internal sealed partial class ClientCredentialsTokenHandler(
    ITokenEndpointService tokenEndpointService,
    IClientCredentialsTokenCache tokenCache,
    IDPoPProofService dpopProofService,
    IOptionsMonitor<ClientCredentialsOptions> clientCredentialsOptionsMonitor,
    IOptions<TokenManagementOptions> tokenManagementOptions,
    IClock clock,
    TokenManagementMetrics metrics,
    ILogger<ClientCredentialsTokenHandler> logger) : DelegatingHandler
{
    internal string ClientName { get; set; } = string.Empty;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Each named client gets its own ClientCredentialsOptions
        ClientCredentialsOptions options = clientCredentialsOptionsMonitor.Get(ClientName);
        // ...
    }
}
```

Registration uses the named overload:

```csharp title="TokenManagementServiceCollectionExtensions.cs"
public static IHttpClientBuilder AddClientCredentialsHttpClient(
    this IServiceCollection services,
    string name,
    Action<ClientCredentialsOptions> configure)
{
    services.AddGranitTokenManagement();
    services.Configure(name, configure); // Named configuration

    return services.AddGranitHttpClient(name)
        .AddHttpMessageHandler(sp =>
        {
            var handler = ActivatorUtilities
                .CreateInstance<ClientCredentialsTokenHandler>(sp);
            handler.ClientName = name;
            return handler;
        });
}
```

### When to use it

- **Background services** and other singletons that need live configuration
- **Named options** in singleton scope (where `IOptionsSnapshot<T>` cannot be injected)
- **Change notification** — subscribe to `OnChange` for cache invalidation or reconnection logic

### When NOT to use it

- Simple static configuration where `IOptions<T>` suffices — `IOptionsMonitor<T>` adds watcher overhead that is unnecessary if the value never changes

## PostConfigure: computed configuration that depends on other services

Sometimes the value you need is not in `appsettings.json` — it depends on the environment, another options instance, or a runtime service. `PostConfigure` runs **after** all `Configure` calls, letting you compute or override values:

```csharp title="ObservabilityServiceCollectionExtensions.cs"
builder.Services
    .AddOptions<ObservabilityOptions>()
    .BindConfiguration(ObservabilityOptions.SectionName)
    .PostConfigure<IHostEnvironment, IConfiguration>((opts, env, config) =>
        ApplyFallbacks(opts, env.ApplicationName, env.EnvironmentName,
            config["OTEL_EXPORTER_OTLP_ENDPOINT"]))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

Here, `PostConfigure` injects `IHostEnvironment` and `IConfiguration` to compute fallback values — the service name defaults to the assembly name, the OTLP endpoint defaults to the environment variable. The caller only needs to set what they want to override.

### `IPostConfigureOptions<T>`: the class-based variant

When post-configuration logic is complex or needs DI-injected services, implement `IPostConfigureOptions<T>`:

```csharp title="DatabaseSigningKeyPostConfigure.cs"
internal sealed partial class DatabaseSigningKeyPostConfigure(
    IServiceScopeFactory scopeFactory,
    IOptions<GranitKeyRotationOptions> rotationOptions,
    ILogger<DatabaseSigningKeyPostConfigure> logger)
    : IPostConfigureOptions<OpenIddictServerOptions>
{
    public void PostConfigure(string? name, OpenIddictServerOptions options)
    {
        if (!rotationOptions.Value.Enabled) return;

        using IServiceScope scope = scopeFactory.CreateScope();
        ISigningKeyStore keyStore = scope.ServiceProvider
            .GetRequiredService<ISigningKeyStore>();

        IReadOnlyList<SigningKey> keys = keyStore
            .GetKeysAsync(SigningKeyStatus.Active, SigningKeyStatus.Retired)
            .GetAwaiter().GetResult();

        // Load signing keys from database into OpenIddict at startup
        foreach (SigningKey key in keys)
        {
            options.SigningCredentials.Add(key.ToSigningCredentials());
        }
    }
}
```

This loads OpenIddict signing keys from the database — a dependency that cannot be expressed via `BindConfiguration`. The `IPostConfigureOptions<T>` interface gives you full DI access.

### `PostConfigureAll<T>`: environment-dependent defaults

When the same override applies to **all** named instances:

```csharp title="GranitBffEndpointsModule.cs"
bool isDevelopment = context.Builder!.Environment.IsDevelopment();

context.Services.PostConfigureAll<BffFrontendOptions>(options =>
{
    options.CookiePrefix = isDevelopment ? "." : "__Host-";
});
```

In development, cookies use a simple `.` prefix (works without HTTPS). In production, `__Host-` enforces Secure + Path=/ + no Domain — a browser-level security guarantee. The application code never checks the environment — the prefix is computed once and applied everywhere.

## Validation: fail fast, not at 3 AM

Options without validation are a time bomb. A missing URL, an invalid TTL, a negative retry count — all of these work fine in `appsettings.json` but explode at runtime, usually in production, usually at night.

### Data annotations

The simplest validation — decorate properties and chain `.ValidateDataAnnotations()`:

```csharp title="ApiDocumentationOptions.cs"
public sealed class ApiDocumentationOptions
{
    public const string SectionName = "ApiDocumentation";

    [Required]
    public string Title { get; set; } = "API";

    public string? Description { get; set; }

    [EmailAddress]
    public string? ContactEmail { get; set; }
}
```

```csharp title="Registration"
builder.Services
    .AddOptions<ApiDocumentationOptions>()
    .BindConfiguration(ApiDocumentationOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

### `IValidateOptions<T>`: custom validation logic

When annotations are not enough — cross-property validation, conditional rules, URI format checks:

```csharp title="GranitBffOptionsValidator.cs"
internal sealed class GranitBffOptionsValidator
    : IValidateOptions<GranitBffOptions>
{
    public ValidateOptionsResult Validate(string? name, GranitBffOptions options)
    {
        List<string> failures = [];

        if (options.Authority is null)
        {
            failures.Add(
                "Authority must not be null — set the OIDC authority URL " +
                "in the 'Bff' configuration section.");
        }

        ValidateCsrfHmacKey(options, failures);
        ValidateFrontends(options, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
```

Registered alongside the options:

```csharp title="Registration"
builder.Services
    .AddOptions<GranitBffOptions>()
    .BindConfiguration("Bff")
    .ValidateOnStart();

builder.Services.AddSingleton<IValidateOptions<GranitBffOptions>,
    GranitBffOptionsValidator>();
```

### `ValidateOnStart()`: the non-negotiable

Without `.ValidateOnStart()`, validation runs **lazily** — only when the options are first resolved. If no code path touches the options during startup, the invalid configuration sits dormant until a user hits the affected endpoint.

`.ValidateOnStart()` forces validation during `IHost.StartAsync()`. The application **refuses to start** with invalid configuration. You find out in CI, not in production.

Across Granit's 128 packages, every options registration that binds configuration uses `ValidateOnStart()`. No exceptions.

## The decision matrix

| Question | `IOptions<T>` | `IOptionsSnapshot<T>` | `IOptionsMonitor<T>` |
| ---------- | :---: | :---: | :---: |
| Can inject into singletons? | Yes | **No** | Yes |
| Can inject into scoped services? | Yes | Yes | Yes |
| Picks up runtime config changes? | **No** | Yes (per scope) | Yes (immediate) |
| Supports named options? | **No** | Yes | Yes |
| Supports `OnChange` callback? | **No** | **No** | Yes |
| Allocation per resolution | Zero (cached) | One per scope | Zero (cached) |
| Best for | Static config | Per-request config | Background services, live reload |

The default choice should be `IOptions<T>`. Upgrade to `IOptionsMonitor<T>` when you need live reload or named options in a singleton. Use `IOptionsSnapshot<T>` only when you specifically need per-request consistency with automatic reload — and you are injecting into a scoped service.

## Key takeaways

- **`IOptions<T>`** is a singleton cache — read once, never reloaded. Use it for static configuration and always combine with `ValidateOnStart()`.
- **`IOptionsSnapshot<T>`** is scoped — fresh per request, consistent within. Use it when configuration changes between requests and you need per-scope consistency.
- **`IOptionsMonitor<T>`** is a singleton with live reload — use it in background services, for named options, and when you need `OnChange` notifications.
- **`PostConfigure`** computes values that depend on other services or the environment. The class-based `IPostConfigureOptions<T>` gives full DI access.
- **`ValidateOnStart()`** is non-negotiable. Fail at startup, not at 3 AM.
- **Default to `IOptions<T>`**, upgrade only when you have a concrete reason. The simplest abstraction that solves your problem is the right one.

## Further reading

- [Configuration & Settings reference](/dotnet/core/configuration/) — Granit configuration architecture, provider chain, environment overrides
- [[LoggerMessage] Over String Interpolation](/blog/loggermessage-over-string-interpolation/) — another "the framework gives you better tools" best-practice
- [Guard Clauses Done Right](/blog/guard-clauses-done-right/) — fail-fast validation at the method level, complementing fail-fast at the config level
