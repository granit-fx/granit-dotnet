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
using Microsoft.Extensions.DependencyInjection;
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

        // Canonical user-session management (formerly Granit.UserSessions + the
        // Granit.Identity.UserSessions bridge): the orchestrator plus the identity-provider
        // session/device providers, made the active backend for /sessions and /devices over
        // IIdentitySessionManager. A BFF deployment replaces these with its own providers.
        context.Services.TryAddScoped<IUserSessionManager, DefaultUserSessionManager>();
        context.Services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider, IdentityUserSessionProvider>());
        context.Services.Replace(ServiceDescriptor.Scoped<IUserDeviceProvider, IdentityUserDeviceProvider>());

        // ADR-051 — User aggregate primitives (Query / Export / EntityDefinition).
        // The IUserDirectoryQueryableSource implementation lives in the EF Core
        // companion package; this module exposes the abstractions only.
        context.Services.AddQueryDefinition<User, UserQueryDefinition>();
        context.Services.AddExportDefinition<User, UserExportDefinition>();
        context.Services.AddImportDefinition<User, UserImportDefinition>();
        context.Services.AddEntityDefinition<User, UserEntityDefinition>();
    }
}
