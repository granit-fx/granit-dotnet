using Granit.Http.Idempotency.Extensions;
using Granit.Modularity;

namespace Granit.Http.Idempotency;

/// <summary>
/// Granit module for HTTP idempotency middleware.
/// Registers <see cref="Abstractions.IIdempotencyStore"/> (in-memory Development default —
/// install <c>Granit.Http.Idempotency.StackExchangeRedis</c> for production),
/// <see cref="Internal.IdempotencyMiddleware"/>, and all required dependencies from
/// configuration section <c>"Http:Idempotency"</c>.
/// </summary>
public sealed class GranitHttpIdempotencyModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitIdempotency();
}
