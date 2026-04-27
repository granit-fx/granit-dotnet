using Granit.DataFiltering;
using Granit.Domain;
using Granit.Encryption;
using Granit.Guids;
using Granit.Mergeable;
using Granit.Mergeable.EntityFrameworkCore;
using Granit.Mergeable.EntityFrameworkCore.Internal;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;
using Granit.Parties.EntityFrameworkCore;
using Granit.Parties.EntityFrameworkCore.Internal;
using Granit.Parties.Mergeable.Internal;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Parties.Mergeable.Tests.Integration;

/// <summary>
/// End-to-end integration test for the full Party merge pipeline: EfMergeService&lt;Party&gt;
/// orchestrator + PartyMergeableAggregateAdapter + PartyParentReferenceRewriter +
/// PartyChildrenReferenceRewriter, all wired against a real PostgreSQL container in a single
/// TransactionScope.
/// </summary>
/// <remarks>
/// <para>
/// Until this test landed, the fake-aggregate orchestrator tests in
/// <c>Granit.Mergeable.EntityFrameworkCore.Tests</c> never exercised the EF
/// <see cref="DbContext.ChangeTracker"/> against a Party loaded with <c>Include(...)</c>, and
/// the rewriter integration tests exercised each rewriter in isolation. The combination
/// (orchestrator + adapter + bulk-SQL rewriters) hides a concrete pitfall: calling
/// <c>db.Parties.Update(party)</c> on a survivor / loser whose navigation graph was loaded
/// via Include — the default semantic of <c>Update()</c> cascades through the whole graph
/// and forces every PartyAddress / PartyEmail / PartyPhone / PartyExternalMapping back into
/// EF tracking with state Modified, snapshotting the shadow FK <c>PartyId</c> at the
/// pre-merge value. <c>SaveChangesAsync</c> would then issue
/// <c>UPDATE … SET PartyId = loserId WHERE Id = childId</c> for every relocated row,
/// silently undoing the bulk rewriter that ran earlier in the same transaction.
/// </para>
/// <para>
/// The current adapter implementation uses <see cref="ChangeTracker.TrackGraph"/> with the
/// root forced Modified and every navigation child forced Unchanged — the assertions below
/// are the regression net for that contract.
/// </para>
/// </remarks>
public sealed class EfMergeServicePartyEndToEndTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private readonly IDataFilter _dataFilter = Substitute.For<IDataFilter>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IGuidGenerator _guidGenerator = SimpleGuidGenerator.Instance;
    private TestPartiesDbContextFactory _partiesFactory = null!;
    private TestMergeableDbContextFactory _mergeableFactory = null!;
    private EfMergeService<Party> _sut = null!;

    public EfMergeServicePartyEndToEndTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        _dataFilter.Disable<IHasMergeTombstone>().Returns(_ => Substitute.For<IDisposable>());
        _clock.Now.Returns(new DateTimeOffset(2026, 4, 27, 12, 0, 0, TimeSpan.Zero));
    }

    public async ValueTask InitializeAsync()
    {
        _partiesFactory = new TestPartiesDbContextFactory(_postgres.ConnectionString);
        _mergeableFactory = new TestMergeableDbContextFactory(_postgres.ConnectionString);

        await using (PartiesDbContext partiesDb = await _partiesFactory.CreateDbContextAsync(TestContext.Current.CancellationToken))
        {
            await partiesDb.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            string p = GranitPartiesDbProperties.DbTablePrefix;
            await partiesDb.Database.ExecuteSqlRawAsync(
                $"TRUNCATE TABLE {p}external_mappings, {p}addresses, {p}emails, {p}phones, {p}parties RESTART IDENTITY CASCADE;",
                TestContext.Current.CancellationToken);
        }

        // EnsureCreatedAsync for a second DbContext on the same database is a no-op once the
        // first context's tables exist — EF interprets the database as "already created" and
        // skips schema generation. Hand-issue the merge-orchestrator's schema + table with
        // IF NOT EXISTS guards so the granit.merge_idempotency table exists for the
        // orchestrator's idempotency cache writes.
        await using (MergeableDbContext mergeableDb = await _mergeableFactory.CreateDbContextAsync(TestContext.Current.CancellationToken))
        {
            await mergeableDb.Database.ExecuteSqlRawAsync(
                """
                CREATE SCHEMA IF NOT EXISTS granit;
                CREATE TABLE IF NOT EXISTS granit.merge_idempotency (
                    "Id" uuid NOT NULL,
                    "TenantId" uuid NULL,
                    "Key" character varying(128) NOT NULL,
                    "RequestHash" character varying(64) NOT NULL,
                    "SurvivorId" uuid NOT NULL,
                    "LoserId" uuid NOT NULL,
                    "ResultJson" text NOT NULL,
                    "ResultMac" character varying(64) NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_merge_idempotency" PRIMARY KEY ("Id")
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_merge_idempotency_TenantId_Key_RequestHash"
                    ON granit.merge_idempotency ("TenantId", "Key", "RequestHash");
                CREATE INDEX IF NOT EXISTS "IX_merge_idempotency_TenantId_SurvivorId_LoserId"
                    ON granit.merge_idempotency ("TenantId", "SurvivorId", "LoserId");
                CREATE INDEX IF NOT EXISTS "IX_merge_idempotency_CreatedAt"
                    ON granit.merge_idempotency ("CreatedAt");
                """,
                TestContext.Current.CancellationToken);
            await mergeableDb.Database.ExecuteSqlRawAsync(
                "TRUNCATE TABLE granit.merge_idempotency RESTART IDENTITY;",
                TestContext.Current.CancellationToken);
        }

        var adapter = new PartyMergeableAggregateAdapter(_partiesFactory, _dataFilter);
        var parentRewriter = new PartyParentReferenceRewriter(_partiesFactory, _dataFilter);
        var childrenRewriter = new PartyChildrenReferenceRewriter(_partiesFactory, _dataFilter);
        IReferenceRewriter<Party>[] rewriters = [parentRewriter, childrenRewriter];

        IStringEncryptionService encryption = new FakeStringEncryption();
        IMergeableSecretProvider secretProvider = new FakeSecretProvider();
        IOptions<MergeableOptions> options = Options.Create(new MergeableOptions());

        _sut = new EfMergeService<Party>(
            adapter,
            rewriters,
            _mergeableFactory,
            _guidGenerator,
            _clock,
            encryption,
            secretProvider,
            options);
    }

    private sealed class FakeStringEncryption : IStringEncryptionService
    {
        public string Encrypt(string plainText) =>
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plainText));

        public string? Decrypt(string cipherText)
        {
            try { return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cipherText)); }
            catch { return null; }
        }
    }

    private sealed class FakeSecretProvider : IMergeableSecretProvider
    {
        private readonly byte[] _key = new byte[32]
        {
            0xab, 0xcd, 0xef, 0x01, 0x23, 0x45, 0x67, 0x89,
            0xab, 0xcd, 0xef, 0x01, 0x23, 0x45, 0x67, 0x89,
            0xab, 0xcd, 0xef, 0x01, 0x23, 0x45, 0x67, 0x89,
            0xab, 0xcd, 0xef, 0x01, 0x23, 0x45, 0x67, 0x89,
        };
        public byte[] GetMacKey() => _key;
    }

    public async ValueTask DisposeAsync()
    {
        await _partiesFactory.DisposeAsync();
        await _mergeableFactory.DisposeAsync();
    }

    [Fact]
    public async Task MergeAsync_PersistsTombstoneOnLoser_AndRetainsRewrittenChildFKs()
    {
        // Arrange — survivor with two emails; loser with one unique email + one duplicate.
        Party survivor = NewParty("Acme S");
        survivor.AddEmail(Guid.NewGuid(), "ops@acme.com", isPrimary: true);
        survivor.AddEmail(Guid.NewGuid(), "shared@acme.com", isPrimary: false);

        Party loser = NewParty("Acme L");
        loser.AddEmail(Guid.NewGuid(), "shared@acme.com", isPrimary: false);     // structural dup → deleted
        loser.AddEmail(Guid.NewGuid(), "billing@acme.com", isPrimary: false);    // unique → relocated

        await SeedAsync(survivor, loser);

        // Act
        MergeResult<Party> result = await _sut.MergeAsync(
            new MergeRequest(survivor.Id, loser.Id, MergeFieldChoices.Empty),
            TestContext.Current.CancellationToken);

        // Assert — orchestrator returns the merged survivor.
        result.DryRun.ShouldBeFalse();
        result.Merged.ShouldNotBeNull();
        result.Merged!.Id.ShouldBe(survivor.Id);

        // Assert — loser is tombstoned in DB (via adapter.ApplyTombstone + PersistMergedPair).
        await using PartiesDbContext assertDb = await _partiesFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        Party persistedLoser = await assertDb.Parties
            .IgnoreQueryFilters()
            .FirstAsync(p => p.Id == loser.Id, TestContext.Current.CancellationToken);
        persistedLoser.MergedIntoId.ShouldBe(survivor.Id);
        persistedLoser.MergedAt.ShouldNotBeNull();

        // Assert — REGRESSION GUARD for the Update()-cascade pitfall: the unique loser email
        // ("billing@acme.com") MUST end up attached to the survivor, with its shadow FK still
        // pointing at survivor.Id after SaveChanges. If the adapter ever reverts to
        // db.Parties.Update(loser), this email's PartyId would be silently rewritten back to
        // loser.Id, breaking the merge contract.
        List<string> survivorEmails = await assertDb.Set<PartyEmail>()
            .Where(e => EF.Property<Guid>(e, "PartyId") == survivor.Id)
            .Select(e => e.Address)
            .OrderBy(s => s)
            .ToListAsync(TestContext.Current.CancellationToken);
        survivorEmails.ShouldBe(["billing@acme.com", "ops@acme.com", "shared@acme.com"]);

        int loserEmailCount = await assertDb.Set<PartyEmail>()
            .CountAsync(e => EF.Property<Guid>(e, "PartyId") == loser.Id, TestContext.Current.CancellationToken);
        loserEmailCount.ShouldBe(0);
    }

    [Fact]
    public async Task MergeAsync_RewritesAllFourChildCollections_AndReParentsChildren()
    {
        // Arrange — full child set on the loser side and a child Party that points at it.
        Party survivor = NewParty("Acme S");
        Party loser = NewParty("Acme L");
        loser.AddEmail(Guid.NewGuid(), "billing@acme.com");
        loser.AddPhone(Guid.NewGuid(), PhoneKind.Mobile, "+3221111111");
        loser.AddAddress(
            Guid.NewGuid(), AddressKind.Billing,
            Address.Create("Rue 1", "Brussels", "1000", "BE"));
        loser.AddExternalMapping(Guid.NewGuid(), "stripe", "cus_loser_123");

        Party child = NewParty("Acme Child");
        child.AttachToParent(PartyId.Create(loser.Id), parentTenantId: null);

        await SeedAsync(survivor, loser, child);

        // Act
        MergeResult<Party> result = await _sut.MergeAsync(
            new MergeRequest(survivor.Id, loser.Id, MergeFieldChoices.Empty),
            TestContext.Current.CancellationToken);

        // Assert — both rewriters reported activity.
        result.RewriteCounts["Party.Children"].ShouldBeGreaterThan(0);
        result.RewriteCounts["Party.ParentContactId"].ShouldBe(1);

        // Assert — every child collection now belongs to the survivor.
        await using PartiesDbContext assertDb = await _partiesFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        (await assertDb.Set<PartyEmail>()
            .CountAsync(e => EF.Property<Guid>(e, "PartyId") == survivor.Id, TestContext.Current.CancellationToken))
            .ShouldBe(1);
        (await assertDb.Set<PartyPhone>()
            .CountAsync(p => EF.Property<Guid>(p, "PartyId") == survivor.Id, TestContext.Current.CancellationToken))
            .ShouldBe(1);
        (await assertDb.Set<PartyAddress>()
            .CountAsync(a => EF.Property<Guid>(a, "PartyId") == survivor.Id, TestContext.Current.CancellationToken))
            .ShouldBe(1);
        (await assertDb.Set<PartyExternalMapping>()
            .CountAsync(m => EF.Property<Guid>(m, "PartyId") == survivor.Id, TestContext.Current.CancellationToken))
            .ShouldBe(1);

        // Assert — child Party is re-parented onto the survivor.
        Party persistedChild = await assertDb.Parties
            .FirstAsync(p => p.Id == child.Id, TestContext.Current.CancellationToken);
        persistedChild.ParentContactId.ShouldNotBeNull();
        persistedChild.ParentContactId.Value.ShouldBe(survivor.Id);
    }

    [Fact]
    public async Task MergeAsync_DryRun_PreviewsConflictsAndCounts_WithoutCommitting()
    {
        Party survivor = NewParty("Acme S");
        Party loser = NewParty("Acme L"); // different Name → 1 conflict
        loser.AddEmail(Guid.NewGuid(), "billing@acme.com");
        await SeedAsync(survivor, loser);

        MergeResult<Party> result = await _sut.MergeAsync(
            new MergeRequest(survivor.Id, loser.Id, MergeFieldChoices.Empty, DryRun: true),
            TestContext.Current.CancellationToken);

        result.DryRun.ShouldBeTrue();
        result.Merged.ShouldBeNull();
        result.Conflicts.ShouldContain(c => c.FieldPath == "Name");
        result.RewriteCounts["Party.Children"].ShouldBe(1);

        // Nothing committed: both parties are still alive (no tombstone), email still on loser.
        await using PartiesDbContext assertDb = await _partiesFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        Party persistedLoser = await assertDb.Parties
            .IgnoreQueryFilters()
            .FirstAsync(p => p.Id == loser.Id, TestContext.Current.CancellationToken);
        persistedLoser.MergedIntoId.ShouldBeNull();
        (await assertDb.Set<PartyEmail>()
            .CountAsync(e => EF.Property<Guid>(e, "PartyId") == loser.Id, TestContext.Current.CancellationToken))
            .ShouldBe(1);
    }

    [Fact]
    public async Task MergeAsync_IdempotentReplay_ReturnsCachedResult_WithRehydratedSurvivor()
    {
        Party survivor = NewParty("Acme S");
        Party loser = NewParty("Acme L");
        loser.AddEmail(Guid.NewGuid(), "billing@acme.com");
        await SeedAsync(survivor, loser);

        var request = new MergeRequest(survivor.Id, loser.Id, MergeFieldChoices.Empty,
            IdempotencyKey: "merge-retry-key-001");

        // First call: live merge.
        MergeResult<Party> first = await _sut.MergeAsync(request, TestContext.Current.CancellationToken);
        first.Merged.ShouldNotBeNull();

        // Second call: replay.
        MergeResult<Party> replay = await _sut.MergeAsync(request, TestContext.Current.CancellationToken);
        replay.DryRun.ShouldBeFalse();
        replay.Merged.ShouldNotBeNull("idempotency replay must rehydrate Merged from the live aggregate");
        replay.Merged!.Id.ShouldBe(survivor.Id);
        replay.RewriteCounts["Party.Children"].ShouldBe(first.RewriteCounts["Party.Children"]);
    }

    private async Task SeedAsync(params Party[] parties)
    {
        await using PartiesDbContext db = await _partiesFactory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        db.Parties.AddRange(parties);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static Party NewParty(string name) =>
        Party.Create(Guid.NewGuid(), tenantId: null, PartyKind.Company, name, "EUR");
}
