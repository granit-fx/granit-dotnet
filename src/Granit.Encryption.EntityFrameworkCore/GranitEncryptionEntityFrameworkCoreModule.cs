using Granit.Core.Modularity;
using Granit.Encryption.EntityFrameworkCore.Interceptors;
using Granit.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Encryption.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core field-level encryption.
/// Provides <see cref="EncryptedAttribute"/> and <see cref="EncryptedStringConverter"/>
/// backed by <see cref="IStringEncryptionService"/>.
/// </summary>
/// <remarks>
/// When <see cref="IEntityEncryptionKeyStore"/> is registered (by a Vault provider),
/// also registers the <see cref="EncryptionIsolationSaveChangesInterceptor"/> and
/// <see cref="EncryptionIsolationMaterializationInterceptor"/> for per-entity key isolation.
/// </remarks>
[DependsOn(
    typeof(GranitEncryptionModule),
    typeof(GranitPersistenceModule))]
public sealed class GranitEncryptionEntityFrameworkCoreModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Interceptors for per-entity key isolation (require IEntityEncryptionKeyStore)
        context.Services.TryAddScoped<EncryptionIsolationSaveChangesInterceptor>();
        context.Services.TryAddScoped<EncryptionIsolationMaterializationInterceptor>();
    }
}
