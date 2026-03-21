using Granit.Core.Modularity;
using Granit.MultiTenancy;
using Granit.OpenIddict.EntityFrameworkCore.Internal;
using Granit.OpenIddict.EntityFrameworkCore.Seeding;
using Granit.OpenIddict.Options;
using Granit.OpenIddict.Services;
using Granit.Persistence;
using Granit.Persistence.DataSeeding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.OpenIddict.EntityFrameworkCore;

/// <summary>
/// Granit module that registers EF Core persistence for OpenIddict.
/// </summary>
/// <remarks>
/// <para>
/// The host application must configure the <see cref="Internal.OpenIddictDbContext"/>
/// connection string via <c>AddGranitOpenIddictEntityFrameworkCore(configure)</c>.
/// This module registers the store implementations, OpenIddict core services,
/// the declarative seed contributor, and passkey options.
/// </para>
/// <para>
/// Depends on <see cref="GranitMultiTenancyModule"/> for GDPR-strict tenant isolation
/// in OpenIddict stores.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitMultiTenancyModule),
    typeof(GranitOpenIddictModule),
    typeof(GranitPersistenceModule))]
public sealed class GranitOpenIddictEntityFrameworkCoreModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services
            .AddOptions<GranitOpenIddictSeedingOptions>()
            .BindConfiguration(GranitOpenIddictSeedingOptions.SectionName);

        context.Services
            .AddOptions<GranitPasskeyOptions>()
            .BindConfiguration(GranitPasskeyOptions.SectionName);

        context.Services.AddTransient<IDataSeedContributor, OpenIddictSeedContributor>();

        context.Services.TryAddScoped<ExternalClaimsMapper>();
        context.Services.TryAddScoped<IExternalLoginService, AspNetExternalLoginService>();
    }
}
