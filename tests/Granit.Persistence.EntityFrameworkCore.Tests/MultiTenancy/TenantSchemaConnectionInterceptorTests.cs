// =============================================================================
// Tests - TenantSchemaConnectionInterceptor
// =============================================================================
// Vérifie que ITenantSchemaActivator est invoqué inconditionnellement à chaque
// ouverture de connexion, y compris sur une connexion recyclée depuis le pool.
//
// Les connexions et commandes sont mockées via NSubstitute — aucune base réelle requise.
// =============================================================================

using System.Data.Common;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests.MultiTenancy;

public sealed class TenantSchemaConnectionInterceptorTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static ICurrentTenant MakeTenant(Guid? id)
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns(id);
        tenant.IsAvailable.Returns(id.HasValue);
        return tenant;
    }

    private static ITenantSchemaProvider MakeProvider(Guid tenantId, string schemaName)
    {
        ITenantSchemaProvider provider = Substitute.For<ITenantSchemaProvider>();
#pragma warning disable CA2012 // NSubstitute setup pattern
        provider.GetSchemaNameAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(schemaName));
#pragma warning restore CA2012
        return provider;
    }

    private static ITenantSchemaActivator MakeActivator() =>
        Substitute.For<ITenantSchemaActivator>();

    private static DbConnection MakeConnection() =>
        Substitute.For<DbConnection>();

    private static ConnectionEndEventData MakeEventData()
    {
        FallbackEventDefinition eventDef = new(
            Substitute.For<ILoggingOptions>(),
            new EventId(0),
            LogLevel.None,
            "TestEvent",
            string.Empty);
        return new(
            eventDef,
            static (_, _) => string.Empty,
            Substitute.For<DbConnection>(),
            null,
            Guid.NewGuid(),
            false,
            DateTimeOffset.UtcNow,
            TimeSpan.Zero);
    }

    // -----------------------------------------------------------------------
    // ConnectionOpenedAsync — tenant actif → ActivateSchemaAsync appelé
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ConnectionOpenedAsync_WhenTenantActive_CallsActivateSchemaAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DbConnection conn = MakeConnection();
        ITenantSchemaActivator activator = MakeActivator();
        TenantSchemaConnectionInterceptor interceptor = new(
            MakeTenant(TenantA),
            MakeProvider(TenantA, "tenant_a"),
            activator);

        await interceptor.ConnectionOpenedAsync(conn, MakeEventData(), cancellationToken);

        await activator.Received(1).ActivateSchemaAsync(conn, "tenant_a", cancellationToken);
    }

    // -----------------------------------------------------------------------
    // Sécurité pool — connexion recyclée (ActivateSchemaAsync ré-exécuté)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ConnectionOpenedAsync_RecycledConnection_ReExecutesActivateSchema()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DbConnection conn = MakeConnection();

        // Tenant A utilise la connexion, puis elle retourne au pool.
        ITenantSchemaActivator activatorA = MakeActivator();
        TenantSchemaConnectionInterceptor interceptorA = new(
            MakeTenant(TenantA),
            MakeProvider(TenantA, "tenant_a"),
            activatorA);
        await interceptorA.ConnectionOpenedAsync(conn, MakeEventData(), cancellationToken);

        // Tenant B récupère la même connexion physique depuis le pool.
        ITenantSchemaActivator activatorB = MakeActivator();
        TenantSchemaConnectionInterceptor interceptorB = new(
            MakeTenant(TenantB),
            MakeProvider(TenantB, "tenant_b"),
            activatorB);
        await interceptorB.ConnectionOpenedAsync(conn, MakeEventData(), cancellationToken);

        // Chaque intercepteur a invoqué son activateur exactement une fois.
        await activatorA.Received(1).ActivateSchemaAsync(conn, "tenant_a", cancellationToken);
        await activatorB.Received(1).ActivateSchemaAsync(conn, "tenant_b", cancellationToken);
    }

    // -----------------------------------------------------------------------
    // Pas de tenant → aucun appel à l'activateur
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ConnectionOpenedAsync_WhenNoTenant_DoesNotCallActivator()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DbConnection conn = MakeConnection();
        ITenantSchemaActivator activator = MakeActivator();
        TenantSchemaConnectionInterceptor interceptor = new(
            MakeTenant(null),
            Substitute.For<ITenantSchemaProvider>(),
            activator);

        await interceptor.ConnectionOpenedAsync(conn, MakeEventData(), cancellationToken);

        await activator.DidNotReceiveWithAnyArgs()
            .ActivateSchemaAsync(Arg.Any<DbConnection>(), Arg.Any<string>(), cancellationToken);
    }

    // -----------------------------------------------------------------------
    // ConnectionOpened (synchrone)
    // -----------------------------------------------------------------------

    [Fact]
    public void ConnectionOpened_WhenTenantActive_CallsActivateSchema()
    {
        DbConnection conn = MakeConnection();
        ITenantSchemaActivator activator = MakeActivator();

        TenantSchemaConnectionInterceptor interceptor = new(
            MakeTenant(TenantA),
            MakeProvider(TenantA, "tenant_a"),
            activator);
        interceptor.ConnectionOpened(conn, MakeEventData());

        activator.Received(1).ActivateSchema(conn, "tenant_a");
    }

    [Fact]
    public void ConnectionOpened_WhenNoTenant_DoesNotCallActivator()
    {
        DbConnection conn = MakeConnection();
        ITenantSchemaActivator activator = MakeActivator();
        TenantSchemaConnectionInterceptor interceptor = new(
            MakeTenant(null),
            Substitute.For<ITenantSchemaProvider>(),
            activator);

        interceptor.ConnectionOpened(conn, MakeEventData());

        activator.DidNotReceiveWithAnyArgs()
            .ActivateSchema(Arg.Any<DbConnection>(), Arg.Any<string>());
    }
}
