using Granit.Core.Modularity;
using Granit.Encryption;
using Granit.MultiTenancy;
using Granit.OpenIddict.Entities;
using Granit.OpenIddict.EntityFrameworkCore.Internal;
using Granit.OpenIddict.EntityFrameworkCore.Seeding;
using Granit.OpenIddict.Options;
using Granit.OpenIddict.Services;
using Granit.Persistence;
using Granit.Persistence.DataSeeding;
using Granit.Persistence.ExtraProperties;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenIddict.Server;

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
/// in OpenIddict stores, and <see cref="GranitEncryptionModule"/> for signing key
/// material encryption at rest.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitEncryptionModule),
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
        context.Services.TryAddScoped<ITwoFactorService, AspNetTwoFactorService>();
        context.Services.TryAddScoped<IAccountDeletionService, AspNetAccountDeletionService>();
        context.Services.TryAddScoped<IPasswordResetService, AspNetPasswordResetService>();
        context.Services.TryAddScoped<ISigningKeyStore, EfSigningKeyStore>();
        context.Services.TryAddScoped<IKeyRotationService, KeyRotationService>();
        context.Services.TryAddScoped<IPasskeyService, AspNetPasskeyService>();
        context.Services.TryAddScoped<IImpersonationService, AspNetImpersonationService>();

        context.Services
            .AddOptions<GranitKeyRotationOptions>()
            .BindConfiguration(GranitKeyRotationOptions.SectionName);

        // GranitUser implements IHasExtraProperties — apps can extend with:
        // services.AddExtraPropertyMappings<GranitUser>(o => o.MapProperty<string>("JobTitle", maxLength: 128));
        // The generic ExtraPropertySyncInterceptor in Granit.Persistence handles sync automatically.
        context.Services.AddExtraPropertyInfrastructure();

        // Load signing/encryption keys from DB at startup (replaces ephemeral keys)
        context.Services.AddSingleton<IPostConfigureOptions<OpenIddictServerOptions>,
            DatabaseSigningKeyPostConfigure>();
    }
}
