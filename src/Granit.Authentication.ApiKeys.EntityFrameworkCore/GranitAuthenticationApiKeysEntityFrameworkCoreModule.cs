using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Authentication.ApiKeys.EntityFrameworkCore;

/// <summary>
/// Granit module that registers EF Core persistence for API keys.
/// </summary>
/// <remarks>
/// The <see cref="Internal.AuthenticationApiKeysDbContext"/> must be configured by the host application
/// (connection string). This module only registers the store implementations.
/// </remarks>
[DependsOn(
    typeof(GranitAuthenticationApiKeysModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitAuthenticationApiKeysEntityFrameworkCoreModule : GranitModule;
