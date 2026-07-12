// =============================================================================
// EmbeddedAuditPersistenceTests - atomicity of the embedded audit pipeline
// =============================================================================
// Verifies (embedded mode — the host context maps the audit entities via
// ConfigureAuditingModule(), the REAL AuditingChangeTrackingInterceptor captures
// through the scoped ChangeTrackingCaptureService resolved from the application
// service provider):
//   - A successful business save writes the business row AND the full audit
//     graph (entry + entity change + property changes) in the SAME database,
//     through the same SaveChanges — the standalone AuditingDbContext store
//     (pointed at a different database) stays EMPTY.
//   - The interceptor resolves the scoped capture service through the
//     CoreOptionsExtension.ApplicationServiceProvider fallback (regression
//     guard for the resolver bug where the EF-internal provider lookup alone
//     returned null and audit capture was silently skipped).
//   - A failed commit rolls the audit rows back with the business mutation,
//     and a subsequent successful save on the SAME context/scope writes
//     EXACTLY ONE new audit entry (OnSaveFailed detached the stale graph).
//   - Same guarantee for a DbUpdateConcurrencyException-style failure + retry.
//   - Saves with no auditable changes produce no audit entry and no Eto.
// =============================================================================

using Granit.Auditing.Domain;
using Granit.Events;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Auditing.EntityFrameworkCore.Tests.Internal;

public sealed class EmbeddedAuditPersistenceTests : IDisposable
{
    private readonly EmbeddedAuditingTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    // -------------------------------------------------------------------------
    // Successful save — audit graph rides the host transaction
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EmbeddedSave_WritesBusinessRowAndFullAuditGraphInSameDatabase()
    {
        // Arrange
        await using EmbeddedHostDbContext context = _harness.CreateAuditedHostContext();
        context.Customers.Add(new HostCustomer { Id = 1, Name = "Alice" });

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert — business row landed
        await using EmbeddedHostDbContext verify = _harness.CreatePlainHostContext();
        (await verify.Customers.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);

        // Assert — full audit graph in the SAME database, written by the same save
        AuditEntry entry = (await _harness.GetHostAuditEntriesAsync()).ShouldHaveSingleItem();
        entry.UserId.ShouldBe("test-user");
        entry.Category.ShouldBe(AuditCategory.DataMutation);
        AuditEntityChange change = entry.EntityChanges.ShouldHaveSingleItem();
        change.EntityType.ShouldBe(nameof(HostCustomer));
        change.EntityId.ShouldBe("1");
        change.ChangeType.ShouldBe(AuditChangeType.Created);
        change.PropertyChanges.ShouldContain(p =>
            p.PropertyName == nameof(HostCustomer.Name) &&
            p.OriginalValue == null &&
            p.NewValue == "Alice");
    }

    [Fact]
    public async Task EmbeddedSave_DoesNotWriteToStandaloneAuditStore()
    {
        // Arrange — the pipeline's IDbContextFactory<AuditingDbContext> points at a
        // DIFFERENT (empty) Sqlite database; if the embedded path leaked into the
        // standalone path, rows would appear there.
        await using EmbeddedHostDbContext context = _harness.CreateAuditedHostContext();
        context.Customers.Add(new HostCustomer { Id = 1, Name = "Alice" });

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert — audit landed in the host database, standalone store untouched
        (await _harness.GetHostAuditEntriesAsync()).ShouldHaveSingleItem();
        (await _harness.GetStandaloneAuditEntriesAsync()).ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Regression guard — capture-service resolution through the application SP
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Interceptor_ResolvesScopedCaptureServiceFromApplicationServiceProvider()
    {
        // Regression guard: EF Core's internal service provider does NOT resolve
        // application services by itself — AuditingChangeTrackingInterceptor must fall
        // back through CoreOptionsExtension.ApplicationServiceProvider (wired here via
        // UseApplicationServiceProvider, as UseGranitInterceptors does in production).
        // Before that fallback existed, this exact setup saved the business row but
        // produced ZERO audit entries, silently. This test MUST fail if the resolver
        // reverts to the internal-provider-only lookup.
        await using EmbeddedHostDbContext context = _harness.CreateAuditedHostContext();
        context.Customers.Add(new HostCustomer { Id = 42, Name = "Resolver Canary" });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        (await _harness.GetHostAuditEntriesAsync()).ShouldHaveSingleItem();
    }

    // -------------------------------------------------------------------------
    // Failed commit — audit rows roll back with the business mutation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FailedSave_LeavesNoAuditRows_AndNextSaveOnSameScopeWritesExactlyOneEntry()
    {
        // Arrange — seed a row through the NON-audited context so the unique index
        // can reject the next insert without generating an audit entry of its own.
        await using (EmbeddedHostDbContext seed = _harness.CreatePlainHostContext())
        {
            seed.Customers.Add(new HostCustomer { Id = 1, Name = "duplicate" });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using EmbeddedHostDbContext context = _harness.CreateAuditedHostContext();
        HostCustomer doomed = new() { Id = 2, Name = "duplicate" };
        context.Customers.Add(doomed);

        // Act — the commit fails on the unique index; the staged audit graph rides
        // the same transaction and must vanish with it.
        await Should.ThrowAsync<DbUpdateException>(
            () => context.SaveChangesAsync(TestContext.Current.CancellationToken));

        // Assert — nothing persisted: no audit rows anywhere, no second business row.
        (await _harness.GetHostAuditEntriesAsync()).ShouldBeEmpty();
        (await _harness.GetStandaloneAuditEntriesAsync()).ShouldBeEmpty();
        await using (EmbeddedHostDbContext verify = _harness.CreatePlainHostContext())
        {
            (await verify.Customers.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
        }

        // Act — fix the conflict and retry on the SAME context/scope.
        doomed.Name = "no-longer-duplicate";
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert — exactly ONE audit entry, from the retry only. If OnSaveFailed had
        // not detached the stale staged graph, the retry would have re-saved it on
        // top of the fresh capture (duplicate entries or a PK violation).
        AuditEntry entry = (await _harness.GetHostAuditEntriesAsync()).ShouldHaveSingleItem();
        AuditEntityChange change = entry.EntityChanges.ShouldHaveSingleItem();
        change.EntityId.ShouldBe("2");
        change.ChangeType.ShouldBe(AuditChangeType.Created);
    }

    [Fact]
    public async Task ConcurrencyFailedSave_ThenRetryOnSameScope_WritesExactlyOneAuditEntry()
    {
        // Arrange — seed a row, load it into the audited context, then delete it
        // behind the context's back so the UPDATE affects zero rows and EF raises
        // DbUpdateConcurrencyException (Sqlite has no rowversion; the zero-rows-affected
        // check is the closest optimistic-concurrency failure it can express).
        await using (EmbeddedHostDbContext seed = _harness.CreatePlainHostContext())
        {
            seed.Customers.Add(new HostCustomer { Id = 1, Name = "Seeded" });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using EmbeddedHostDbContext context = _harness.CreateAuditedHostContext();
        HostCustomer tracked = await context.Customers
            .SingleAsync(c => c.Id == 1, TestContext.Current.CancellationToken);
        tracked.Name = "Modified";

        await using (EmbeddedHostDbContext saboteur = _harness.CreatePlainHostContext())
        {
            await saboteur.Customers
                .Where(c => c.Id == 1)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }

        // Act — concurrency failure.
        await Should.ThrowAsync<DbUpdateConcurrencyException>(
            () => context.SaveChangesAsync(TestContext.Current.CancellationToken));

        // Assert — the failed save left no audit rows.
        (await _harness.GetHostAuditEntriesAsync()).ShouldBeEmpty();

        // Act — business retry on the SAME context/scope: give up on the vanished row
        // and insert a replacement instead.
        context.Entry(tracked).State = EntityState.Detached;
        context.Customers.Add(new HostCustomer { Id = 3, Name = "Recovered" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert — exactly one audit entry for the retry; no duplicate or PK violation
        // from the stale staged audit entities of the failed save.
        AuditEntry entry = (await _harness.GetHostAuditEntriesAsync()).ShouldHaveSingleItem();
        AuditEntityChange change = entry.EntityChanges.ShouldHaveSingleItem();
        change.EntityId.ShouldBe("3");
        change.ChangeType.ShouldBe(AuditChangeType.Created);
    }

    // -------------------------------------------------------------------------
    // Empty capture — no auditable changes
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Save_WithOnlyAuditIgnoredChanges_WritesNoAuditEntryAndDispatchesNoEto()
    {
        // Arrange — the only tracked change is an [AuditIgnore] entity.
        await using EmbeddedHostDbContext context = _harness.CreateAuditedHostContext();
        context.IgnoredEntities.Add(new AuditIgnoredHostEntity { Id = 1, Payload = "invisible" });

        // Act
        int affected = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert — the business row saved, but no audit entry and no Eto anywhere.
        affected.ShouldBe(1);
        (await _harness.GetHostAuditEntriesAsync()).ShouldBeEmpty();
        (await _harness.GetStandaloneAuditEntriesAsync()).ShouldBeEmpty();
        await _harness.EventDispatcher.DidNotReceive().DispatchAsync(
            Arg.Any<IReadOnlyList<IIntegrationEvent>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_WithNoChanges_WritesNoAuditEntryAndDispatchesNoEto()
    {
        // Arrange
        await using EmbeddedHostDbContext context = _harness.CreateAuditedHostContext();

        // Act
        int affected = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        affected.ShouldBe(0);
        (await _harness.GetHostAuditEntriesAsync()).ShouldBeEmpty();
        await _harness.EventDispatcher.DidNotReceive().DispatchAsync(
            Arg.Any<IReadOnlyList<IIntegrationEvent>>(),
            Arg.Any<CancellationToken>());
    }
}
