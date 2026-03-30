using Granit.Bff.EntityFrameworkCore.Internal;
using Granit.Encryption;
using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Bff.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core-backed BFF session persistence.
/// Replaces the default <see cref="IDistributedCache"/>-backed <c>IBffTokenStore</c>
/// for deployments without Redis.
/// </summary>
/// <remarks>
/// Token encryption at rest is enabled automatically when <c>GranitEncryptionModule</c>
/// is loaded. Without it, tokens are stored as plaintext JSON (a warning is logged).
/// </remarks>
[DependsOn(
    typeof(GranitBffModule),
    typeof(GranitEncryptionModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitBffEntityFrameworkCoreModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        // Replace the default distributed cache store with EF Core
        context.Services.AddScoped<IBffTokenStore, EfCoreBffTokenStore>();
}
