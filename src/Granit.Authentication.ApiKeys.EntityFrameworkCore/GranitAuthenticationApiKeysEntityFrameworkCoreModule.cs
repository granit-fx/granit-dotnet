using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine.EntityFrameworkCore;

namespace Granit.Authentication.ApiKeys.EntityFrameworkCore;

/// <summary>
/// Granit module that registers EF Core persistence for API keys.
/// </summary>
/// <remarks>
/// The <see cref="Internal.AuthenticationApiKeysDbContext"/> must be configured by the host application
/// (connection string). This module only registers the store implementations and the
/// <c>IQueryableSource&lt;ApiKeyEntry&gt;</c> backing <c>MapGranitQuery&lt;ApiKeyEntry&gt;</c>.
/// </remarks>
[DependsOn(
    typeof(GranitAuthenticationApiKeysModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule),
    typeof(GranitQueryEngineEntityFrameworkCoreModule))]
public sealed class GranitAuthenticationApiKeysEntityFrameworkCoreModule : GranitModule;
