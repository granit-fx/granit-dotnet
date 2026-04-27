using Granit.DataFiltering;
using Granit.Parties.Domain;
using Granit.Parties.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shouldly;
using Xunit;

namespace Granit.Parties.EntityFrameworkCore.Tests.Internal;

/// <summary>
/// Integration tests for <see cref="PartyCanonicalisationInterceptor"/>: verifies that
/// canonical projections are populated on save for Party.TaxId (overwritten in place),
/// PartyEmail.CanonicalEmail (separate column), and PartyPhone.CanonicalNumber (separate
/// column). Uses the InMemory provider — interceptors are honoured the same way as on
/// SQL providers, just without real SQL.
/// </summary>
[Collection(ContactsDbSerialGroup.Name)]
public sealed class PartyCanonicalisationInterceptorTests : IAsyncDisposable
{
    private readonly DataFilter _filter = new();
    private readonly string _dbName = $"canonicalisation-{Guid.NewGuid()}";
    private readonly InMemoryDatabaseRoot _dbRoot = new();
    private readonly StubCurrentTenant _tenant = new();

    public async ValueTask DisposeAsync()
    {
        await using PartiesDbContext db = NewContext();
        await db.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
    }

    private PartiesDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<PartiesDbContext>()
                .UseInMemoryDatabase(_dbName, _dbRoot)
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .EnableServiceProviderCaching(false)
                .AddInterceptors(new PartyCanonicalisationInterceptor())
                .Options,
            _tenant,
            _filter);

    [Fact]
    public async Task SaveAsync_overwrites_TaxId_with_canonical_form_in_place()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var p = Party.Create(
            id: Guid.NewGuid(),
            tenantId: null,
            kind: PartyKind.Company,
            name: "Acme",
            defaultCurrency: "EUR",
            taxId: "BE 0123.456.789");

        await using (PartiesDbContext db = NewContext())
        {
            db.Parties.Add(p);
            await db.SaveChangesAsync(ct);
        }

        await using PartiesDbContext readBack = NewContext();
        Party loaded = (await readBack.Parties.FindAsync([p.Id], ct))!;
        loaded.TaxId.ShouldBe("BE0123456789");
    }

    [Fact]
    public async Task SaveAsync_populates_CanonicalEmail_for_each_PartyEmail()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var p = Party.Create(
            id: Guid.NewGuid(),
            tenantId: null,
            kind: PartyKind.Individual,
            name: "Alice",
            defaultCurrency: "EUR");
        p.AddEmail(Guid.NewGuid(), "Alice+Newsletter@Gmail.com");
        p.AddEmail(Guid.NewGuid(), "alice.smith@example.com");

        await using (PartiesDbContext db = NewContext())
        {
            db.Parties.Add(p);
            await db.SaveChangesAsync(ct);
        }

        await using PartiesDbContext readBack = NewContext();
        Party loaded = await readBack.Parties
            .Include(x => x.Emails)
            .FirstAsync(x => x.Id == p.Id, ct);

        loaded.Emails.ShouldContain(e => e.Address == "Alice+Newsletter@Gmail.com" && e.CanonicalEmail == "alice@gmail.com");
        // Non-Gmail address: only lower-cased + trimmed, no dot/plus stripping.
        loaded.Emails.ShouldContain(e => e.Address == "alice.smith@example.com" && e.CanonicalEmail == "alice.smith@example.com");
    }

    [Fact]
    public async Task SaveAsync_populates_CanonicalNumber_for_each_PartyPhone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var p = Party.Create(
            id: Guid.NewGuid(),
            tenantId: null,
            kind: PartyKind.Individual,
            name: "Bob",
            defaultCurrency: "EUR");
        p.AddPhone(Guid.NewGuid(), PhoneKind.Mobile, "+32 470 12 34 56");
        p.AddPhone(Guid.NewGuid(), PhoneKind.Work, "0470123456"); // domestic — unparseable without region

        await using (PartiesDbContext db = NewContext())
        {
            db.Parties.Add(p);
            await db.SaveChangesAsync(ct);
        }

        await using PartiesDbContext readBack = NewContext();
        Party loaded = await readBack.Parties
            .Include(x => x.Phones)
            .FirstAsync(x => x.Id == p.Id, ct);

        // E.164-prefixed input → strict E.164 form on the canonical column; verbatim preserved.
        loaded.Phones.ShouldContain(ph => ph.Number == "+32 470 12 34 56" && ph.CanonicalNumber == "+32470123456");
        // Domestic input without country code → canonical is null (libphonenumber rejects ambiguous input).
        loaded.Phones.ShouldContain(ph => ph.Number == "0470123456" && ph.CanonicalNumber == null);
    }

    [Fact]
    public async Task SaveAsync_recomputes_CanonicalEmail_when_Address_is_modified()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var emailId = Guid.NewGuid();
        var p = Party.Create(
            id: Guid.NewGuid(),
            tenantId: null,
            kind: PartyKind.Individual,
            name: "Carol",
            defaultCurrency: "EUR");
        p.AddEmail(emailId, "carol@gmail.com");

        await using (PartiesDbContext db = NewContext())
        {
            db.Parties.Add(p);
            await db.SaveChangesAsync(ct);
        }

        // Modify the email address — the interceptor must re-canonicalise on the next save.
        await using (PartiesDbContext db = NewContext())
        {
            Party loaded = await db.Parties.Include(x => x.Emails).FirstAsync(x => x.Id == p.Id, ct);
            loaded.UpdateEmail(emailId, "Carol+Tag@GMAIL.com", label: null);
            await db.SaveChangesAsync(ct);
        }

        await using PartiesDbContext readBack = NewContext();
        Party reloaded = await readBack.Parties.Include(x => x.Emails).FirstAsync(x => x.Id == p.Id, ct);
        reloaded.Emails.ShouldContain(e => e.Address == "Carol+Tag@GMAIL.com" && e.CanonicalEmail == "carol@gmail.com");
    }
}
