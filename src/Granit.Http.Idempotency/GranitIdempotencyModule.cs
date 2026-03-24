using Granit.Caching;
using Granit.Http.Idempotency.Extensions;
using Granit.Modularity;
using Granit.Users;

namespace Granit.Http.Idempotency;

/// <summary>
/// Granit module for HTTP idempotency middleware.
/// Registers <see cref="Abstractions.IIdempotencyStore"/>, <see cref="Internal.IdempotencyMiddleware"/>,
/// and all required dependencies from configuration section <c>"Idempotency"</c>.
/// </summary>
[DependsOn(
    typeof(GranitCachingModule))]
public sealed class GranitIdempotencyModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitIdempotency();
}
