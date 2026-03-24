using Granit.Modularity;
using Granit.Persistence;

namespace Granit.Authentication.ApiKeys.EntityFrameworkCore;

/// <summary>
/// Granit module that registers EF Core persistence for API keys.
/// </summary>
/// <remarks>
/// The <see cref="Internal.ApiKeysDbContext"/> must be configured by the host application
/// (connection string). This module only registers the store implementations.
/// </remarks>
[DependsOn(
    typeof(GranitAuthenticationApiKeysModule),
    typeof(GranitPersistenceModule))]
public sealed class GranitAuthenticationApiKeysEntityFrameworkCoreModule : GranitModule;
