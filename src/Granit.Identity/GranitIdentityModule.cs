using Granit.Auditing;
using Granit.DataExchange.Extensions;
using Granit.Entities.Extensions;
using Granit.Identity.Domain;
using Granit.Identity.Entities;
using Granit.Identity.Exports;
using Granit.Identity.Extensions;
using Granit.Identity.Import;
using Granit.Identity.Internal;
using Granit.Identity.Queries;
using Granit.Modularity;
using Granit.QueryEngine;
using Granit.QueryEngine.Extensions;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Identity;

/// <summary>
/// Granit module for identity provider abstractions.
/// Registers a <see cref="Internal.NullIdentityProvider"/> by default plus the
/// canonical <see cref="User"/> aggregate's QueryDefinition,
/// ExportDefinition, and EntityDefinition (per ADR-051).
/// Install a provider package (e.g. <c>Granit.Identity.Federated.Keycloak</c>)
/// to connect to a real identity system; install
/// <c>Granit.Identity.EntityFrameworkCore</c> to wire the EF Core
/// <see cref="IUserDirectoryQueryableSource"/> impl.
/// </summary>
[DependsOn(typeof(GranitAuditingModule))]
[DependsOn(typeof(GranitIdentityAbstractionsModule))]
[DependsOn(typeof(GranitQueryEngineAbstractionsModule))]
public sealed class GranitIdentityModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddGranitIdentity();

        // Canonical user-session management: the orchestrator behind /sessions and /devices.
        // The session/device BACKEND is contributed by whichever integration package is installed
        // (OpenIddict, Keycloak, a BFF, …), each registering its own IUserSessionProvider /
        // IUserDeviceProvider. With none installed, the Null defaults from the Abstractions module
        // resolve and the canonical API returns an empty list everywhere.
        context.Services.TryAddScoped<IUserSessionManager, DefaultUserSessionManager>();

        // ADR-051 — User aggregate primitives (Query / Export / EntityDefinition).
        // The IUserDirectoryQueryableSource implementation lives in the EF Core
        // companion package; this module exposes the abstractions only.
        context.Services.AddQueryDefinition<User, UserQueryDefinition>();
        context.Services.AddExportDefinition<User, UserExportDefinition>();
        context.Services.AddImportDefinition<User, UserImportDefinition>();
        context.Services.AddEntityDefinition<User, UserEntityDefinition>();
    }
}
