using Granit.Identity.Local.EntityFrameworkCore.Internal;
using Granit.Identity.Local.Services;
using Granit.Modularity;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Identity.Local.EntityFrameworkCore;

/// <summary>
/// Granit module registering EF Core persistence for the local-identity tables.
/// </summary>
/// <remarks>
/// Registers the group store over <see cref="Internal.IdentityLocalDbContext"/> and the dynamic
/// user-property (metadata) infrastructure. The DbContext, ASP.NET Core Identity stores, DbContext
/// accessor and query sources are wired by
/// <c>AddGranitIdentityLocalEntityFrameworkCore</c> (host builder extension).
/// </remarks>
[DependsOn(
    typeof(GranitIdentityLocalModule),
    typeof(GranitMultiTenancyModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitIdentityLocalEntityFrameworkCoreModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddScoped<ILocalIdentityGroupStore, IdentityLocalGroupStore>();

        // LocalIdentity implements IHasMetadata — apps can extend user properties by calling
        // AddMetadataMappings<LocalIdentity> in their own module. AddMetadataInfrastructure registers
        // MetadataSyncInterceptor as IGranitAutoInterceptor so UseGranitInterceptors picks it up.
        context.Services.AddMetadataInfrastructure();
    }
}
