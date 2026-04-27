using Granit.DataFiltering;
using Granit.Domain;
using Granit.Mergeable;
using Granit.Mergeable.Exceptions;
using Granit.Parties.Domain;
using Granit.Parties.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Parties.Mergeable.Internal;

/// <summary>
/// SQL bulk-update rewriter for the four <see cref="Party"/> child collections —
/// <c>PartyAddresses</c>, <c>PartyEmails</c>, <c>PartyPhones</c>, <c>PartyExternalMappings</c>.
/// For each table:
/// <list type="number">
/// <item>Demote primaries / defaults that would clash with the survivor's, before any move.</item>
/// <item>Delete the loser-side rows that are <em>structural duplicates</em> of an existing
/// survivor row (and, for <c>PartyExternalMappings</c>, fail fast on
/// <em>per-provider conflicts</em> — same <c>ProviderName</c> but different
/// <c>ExternalId</c> on both sides).</item>
/// <item>Re-point the remaining loser-side rows to the survivor by rewriting the shadow FK
/// <c>PartyId</c>.</item>
/// <item>Verify the per-aggregate caps (<see cref="Party.MaxAddresses"/>,
/// <see cref="Party.MaxEmails"/>, <see cref="Party.MaxPhones"/>). External mappings have
/// no count cap — the per-provider uniqueness constraint enforced in step 2 makes the
/// post-merge cardinality bounded by the number of distinct providers integrated with
/// the platform.</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// Why a SQL rewriter and not <c>survivor.Children.AddRange(loser.Children)</c>: the four
/// child entities are mapped via <c>HasMany.WithOne().HasForeignKey("PartyId")</c> with a
/// shadow FK and their own primary key. EF Core would either insert duplicate-PK rows or
/// leave the shadow FK pointing at the loser if we tried the in-memory move — neither
/// yields a consistent state. Going through bulk SQL is also significantly faster on
/// parties with hundreds of addresses (rare but not unheard of for global retailers).
/// </para>
/// <para>
/// The merge orchestrator (<see cref="IMergeService{TAggregate}"/>) runs every registered
/// <see cref="IReferenceRewriter{TAggregate}"/> inside the same <see cref="System.Transactions.TransactionScope"/>,
/// so any exception thrown here rolls back the entire merge atomically.
/// </para>
/// </remarks>
internal sealed class PartyChildrenReferenceRewriter(
    IDbContextFactory<PartiesDbContext> contextFactory,
    IDataFilter dataFilter) : IReferenceRewriter<Party>
{
    private const string PartyIdShadowFk = "PartyId";

    /// <inheritdoc />
    public string Description => "Party.Children";

    /// <inheritdoc />
    public async Task<int> RewriteAsync(Guid survivorId, Guid loserId, CancellationToken cancellationToken)
    {
        await using PartiesDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        using IDisposable _ = dataFilter.Disable<IHasMergeTombstone>();

        int total = 0;
        total += await RewriteEmailsAsync(db, survivorId, loserId, cancellationToken).ConfigureAwait(false);
        total += await RewritePhonesAsync(db, survivorId, loserId, cancellationToken).ConfigureAwait(false);
        total += await RewriteAddressesAsync(db, survivorId, loserId, cancellationToken).ConfigureAwait(false);
        total += await RewriteExternalMappingsAsync(db, survivorId, loserId, cancellationToken).ConfigureAwait(false);

        await EnsureCapsAsync(db, survivorId, cancellationToken).ConfigureAwait(false);

        return total;
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(Guid survivorId, Guid loserId, CancellationToken cancellationToken)
    {
        // Dry-run preview: count the loser-side rows that would be touched (deleted or moved)
        // across the four child collections. Cap violations are not checked here — only the
        // live RewriteAsync path enforces them.
        await using PartiesDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        using IDisposable _ = dataFilter.Disable<IHasMergeTombstone>();

        int total = 0;
        total += await db.Set<PartyEmail>()
            .Where(e => EF.Property<Guid>(e, PartyIdShadowFk) == loserId)
            .CountAsync(cancellationToken).ConfigureAwait(false);
        total += await db.Set<PartyPhone>()
            .Where(p => EF.Property<Guid>(p, PartyIdShadowFk) == loserId)
            .CountAsync(cancellationToken).ConfigureAwait(false);
        total += await db.Set<PartyAddress>()
            .Where(a => EF.Property<Guid>(a, PartyIdShadowFk) == loserId)
            .CountAsync(cancellationToken).ConfigureAwait(false);
        total += await db.Set<PartyExternalMapping>()
            .Where(m => EF.Property<Guid>(m, PartyIdShadowFk) == loserId)
            .CountAsync(cancellationToken).ConfigureAwait(false);
        return total;
    }

    private static async Task<int> RewriteEmailsAsync(
        PartiesDbContext db, Guid survivorId, Guid loserId, CancellationToken ct)
    {
        // Demote loser primaries when the survivor already has one (only one IsPrimary per party).
        bool survivorHasPrimary = await db.Set<PartyEmail>()
            .AnyAsync(e => EF.Property<Guid>(e, PartyIdShadowFk) == survivorId && e.IsPrimary, ct)
            .ConfigureAwait(false);
        if (survivorHasPrimary)
        {
            await db.Set<PartyEmail>()
                .Where(e => EF.Property<Guid>(e, PartyIdShadowFk) == loserId && e.IsPrimary)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsPrimary, false), ct)
                .ConfigureAwait(false);
        }

        // Structural duplicates = same Address (case-sensitive in SQL; canonicalisation lives upstream).
        await db.Set<PartyEmail>()
            .Where(e => EF.Property<Guid>(e, PartyIdShadowFk) == loserId
                        && db.Set<PartyEmail>().Any(s =>
                            EF.Property<Guid>(s, PartyIdShadowFk) == survivorId
                            && s.Address == e.Address))
            .ExecuteDeleteAsync(ct).ConfigureAwait(false);

        return await db.Set<PartyEmail>()
            .Where(e => EF.Property<Guid>(e, PartyIdShadowFk) == loserId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(e => EF.Property<Guid>(e, PartyIdShadowFk), survivorId), ct)
            .ConfigureAwait(false);
    }

    private static async Task<int> RewritePhonesAsync(
        PartiesDbContext db, Guid survivorId, Guid loserId, CancellationToken ct)
    {
        bool survivorHasPrimary = await db.Set<PartyPhone>()
            .AnyAsync(p => EF.Property<Guid>(p, PartyIdShadowFk) == survivorId && p.IsPrimary, ct)
            .ConfigureAwait(false);
        if (survivorHasPrimary)
        {
            await db.Set<PartyPhone>()
                .Where(p => EF.Property<Guid>(p, PartyIdShadowFk) == loserId && p.IsPrimary)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsPrimary, false), ct)
                .ConfigureAwait(false);
        }

        // Structural duplicates = same Number (canonical E.164 expected — see PhoneE164 column in dedup epic).
        await db.Set<PartyPhone>()
            .Where(p => EF.Property<Guid>(p, PartyIdShadowFk) == loserId
                        && db.Set<PartyPhone>().Any(s =>
                            EF.Property<Guid>(s, PartyIdShadowFk) == survivorId
                            && s.Number == p.Number))
            .ExecuteDeleteAsync(ct).ConfigureAwait(false);

        return await db.Set<PartyPhone>()
            .Where(p => EF.Property<Guid>(p, PartyIdShadowFk) == loserId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(p => EF.Property<Guid>(p, PartyIdShadowFk), survivorId), ct)
            .ConfigureAwait(false);
    }

    private static async Task<int> RewriteAddressesAsync(
        PartiesDbContext db, Guid survivorId, Guid loserId, CancellationToken ct)
    {
        // Demote loser defaults per AddressKind that the survivor already has a default for.
        // Materialise once: the kind set is small (Billing, Shipping, …) so a join is overkill.
        List<AddressKind> survivorDefaultKinds = await db.Set<PartyAddress>()
            .Where(a => EF.Property<Guid>(a, PartyIdShadowFk) == survivorId && a.IsDefault)
            .Select(a => a.Kind)
            .Distinct()
            .ToListAsync(ct).ConfigureAwait(false);

        if (survivorDefaultKinds.Count > 0)
        {
            await db.Set<PartyAddress>()
                .Where(a => EF.Property<Guid>(a, PartyIdShadowFk) == loserId
                            && a.IsDefault
                            && survivorDefaultKinds.Contains(a.Kind))
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, false), ct)
                .ConfigureAwait(false);
        }

        // Structural duplicates = same Address VO equality components (CompanyName, Line1,
        // Line2, City, PostalCode, State, Country). Stored as owned columns on the same row.
        await db.Set<PartyAddress>()
            .Where(a => EF.Property<Guid>(a, PartyIdShadowFk) == loserId
                        && db.Set<PartyAddress>().Any(s =>
                            EF.Property<Guid>(s, PartyIdShadowFk) == survivorId
                            && s.Value.CompanyName == a.Value.CompanyName
                            && s.Value.Line1 == a.Value.Line1
                            && s.Value.Line2 == a.Value.Line2
                            && s.Value.City == a.Value.City
                            && s.Value.PostalCode == a.Value.PostalCode
                            && s.Value.State == a.Value.State
                            && s.Value.Country == a.Value.Country))
            .ExecuteDeleteAsync(ct).ConfigureAwait(false);

        return await db.Set<PartyAddress>()
            .Where(a => EF.Property<Guid>(a, PartyIdShadowFk) == loserId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(a => EF.Property<Guid>(a, PartyIdShadowFk), survivorId), ct)
            .ConfigureAwait(false);
    }

    private static async Task<int> RewriteExternalMappingsAsync(
        PartiesDbContext db, Guid survivorId, Guid loserId, CancellationToken ct)
    {
        // Conflict: same ProviderName on both sides but different ExternalId — the unique
        // index on (PartyId, ProviderName) would also fail post-move, but a domain-specific
        // exception is more actionable than a database constraint violation. Override
        // semantics live in the endpoint layer (story #1290) — at this layer we fail fast.
        List<string> conflictingProviders = await db.Set<PartyExternalMapping>()
            .Where(m => EF.Property<Guid>(m, PartyIdShadowFk) == loserId
                        && db.Set<PartyExternalMapping>().Any(s =>
                            EF.Property<Guid>(s, PartyIdShadowFk) == survivorId
                            && s.ProviderName == m.ProviderName
                            && s.ExternalId != m.ExternalId))
            .Select(m => m.ProviderName)
            .Distinct()
            .ToListAsync(ct).ConfigureAwait(false);

        if (conflictingProviders.Count > 0)
        {
            throw new MergeException(
                $"External provider mapping conflict on: {string.Join(", ", conflictingProviders)}. "
                + "Resolve via the merge endpoint with an explicit ExternalMappings.<provider> override.");
        }

        // Structural duplicates = same (ProviderName, ExternalId).
        await db.Set<PartyExternalMapping>()
            .Where(m => EF.Property<Guid>(m, PartyIdShadowFk) == loserId
                        && db.Set<PartyExternalMapping>().Any(s =>
                            EF.Property<Guid>(s, PartyIdShadowFk) == survivorId
                            && s.ProviderName == m.ProviderName
                            && s.ExternalId == m.ExternalId))
            .ExecuteDeleteAsync(ct).ConfigureAwait(false);

        return await db.Set<PartyExternalMapping>()
            .Where(m => EF.Property<Guid>(m, PartyIdShadowFk) == loserId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(m => EF.Property<Guid>(m, PartyIdShadowFk), survivorId), ct)
            .ConfigureAwait(false);
    }

    private static async Task EnsureCapsAsync(PartiesDbContext db, Guid survivorId, CancellationToken ct)
    {
        int emailCount = await db.Set<PartyEmail>()
            .CountAsync(e => EF.Property<Guid>(e, PartyIdShadowFk) == survivorId, ct).ConfigureAwait(false);
        if (emailCount > Party.MaxEmails)
        {
            throw new MergeException($"Email cap exceeded after merge: {emailCount} > {Party.MaxEmails}.");
        }

        int phoneCount = await db.Set<PartyPhone>()
            .CountAsync(p => EF.Property<Guid>(p, PartyIdShadowFk) == survivorId, ct).ConfigureAwait(false);
        if (phoneCount > Party.MaxPhones)
        {
            throw new MergeException($"Phone cap exceeded after merge: {phoneCount} > {Party.MaxPhones}.");
        }

        int addressCount = await db.Set<PartyAddress>()
            .CountAsync(a => EF.Property<Guid>(a, PartyIdShadowFk) == survivorId, ct).ConfigureAwait(false);
        if (addressCount > Party.MaxAddresses)
        {
            throw new MergeException($"Address cap exceeded after merge: {addressCount} > {Party.MaxAddresses}.");
        }
    }
}
