using Granit.Encryption.EntityFrameworkCore;
using Granit.Identity.EntityFrameworkCore.Internal;
using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Identity.EntityFrameworkCore;

/// <summary>
/// Granit module that registers EF Core persistence for the canonical
/// <see cref="Domain.User"/> aggregate (ADR-051 B-step 1.5).
/// </summary>
/// <remarks>
/// The DbContext is registered when the host calls
/// <see cref="Extensions.IdentityEntityFrameworkCoreServiceCollectionExtensions.AddGranitIdentityEntityFrameworkCore"/>
/// — this module class only declares the framework dependency edge.
/// Migrations are owned by the consuming app per the framework convention
/// (no migrations live inside framework packages).
/// </remarks>
[DependsOn(
    typeof(GranitEncryptionEntityFrameworkCoreModule),
    typeof(GranitIdentityAbstractionsModule),
    typeof(GranitIdentityModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitIdentityEntityFrameworkCoreModule : GranitModule
{
    /// <inheritdoc />
    // Durable session-security state store (risk verdicts, device trust, behavioural profile, single-use
    // session-review decisions), persisting to the User DbContext. Overrides the in-memory default from
    // Granit.Identity.Abstractions so verdicts, trusted devices, habitual profiles and review idempotency
    // all survive restarts and span instances. The DbContext itself is registered by
    // AddGranitIdentityEntityFrameworkCore.
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddScoped<IIdentitySecurityStateStore, EfCoreIdentitySecurityStateStore>();
}
