using Granit.Encryption.EntityFrameworkCore;
using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Identity.EntityFrameworkCore;

/// <summary>
/// Granit module that registers EF Core persistence for the canonical
/// <see cref="Domain.User"/> aggregate (ADR-051 B-step 1.5).
/// </summary>
/// <remarks>
/// The DbContext is registered when the host calls
/// <see cref="Extensions.IdentityEntityFrameworkCoreHostApplicationBuilderExtensions.AddGranitIdentityEntityFrameworkCore"/>
/// — this module class only declares the framework dependency edge.
/// Migrations are owned by the consuming app per the framework convention
/// (no migrations live inside framework packages).
/// </remarks>
[DependsOn(
    typeof(GranitEncryptionEntityFrameworkCoreModule),
    typeof(GranitIdentityModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitIdentityEntityFrameworkCoreModule : GranitModule;
