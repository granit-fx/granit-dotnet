using Granit.Parties.Domain;
using Granit.Parties.EntityFrameworkCore.Canonicalisation;
using Granit.Persistence.EntityFrameworkCore.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Granit.Parties.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core interceptor that populates the dedup-friendly canonical projections of party
/// identity fields on every save:
/// <list type="bullet">
///   <item><see cref="PartyEmail.CanonicalEmail"/> — derived from <see cref="PartyEmail.Address"/></item>
///   <item><see cref="PartyPhone.CanonicalNumber"/> — derived from <see cref="PartyPhone.Number"/></item>
///   <item><see cref="Party.TaxId"/> — overwritten in place with the canonical form (single
///         column by design — TaxId has a legal canonical shape, no display ambiguity)</item>
/// </list>
/// Powers Tier-1 deterministic duplicate detection (Epic #1280). Auto-wired via
/// <see cref="IGranitAutoInterceptor"/> through the same path as <c>AuditingChangeTrackingInterceptor</c>.
/// </summary>
internal sealed class PartyCanonicalisationInterceptor : SaveChangesInterceptor, IGranitAutoInterceptor
{
    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc/>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Apply(eventData);
        return result;
    }

    private static void Apply(DbContextEventData eventData)
    {
        if (eventData.Context is null)
        {
            return;
        }

        foreach (EntityEntry entry in eventData.Context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            switch (entry.Entity)
            {
                case Party party:
                    ApplyToParty(party);
                    break;
                case PartyEmail email:
                    ApplyToEmail(email);
                    break;
                case PartyPhone phone:
                    ApplyToPhone(phone);
                    break;
            }
        }
    }

    // TaxId — single column overwritten in place. The canonical form IS the legal one
    // (KBO/BCE, HMRC, IRS all accept it without separators), so there is no UX cost to
    // displaying it back to the admin in the canonical shape.
    private static void ApplyToParty(Party party)
    {
        if (party.TaxId is null)
        {
            return;
        }

        string? canonical = TaxIdCanonicaliser.Canonicalise(party.TaxId);
        if (canonical is not null && !string.Equals(canonical, party.TaxId, StringComparison.Ordinal))
        {
            party.OverwriteTaxIdCanonical(canonical);
        }
    }

    // Email — preserved verbatim on Address; canonical projected to CanonicalEmail. UX
    // cost of overwriting Address would be too high (admin would see "alice+x@gmail.com"
    // collapse to "alice@gmail.com" and think the system corrupted their input).
    private static void ApplyToEmail(PartyEmail email)
    {
        string? canonical = EmailCanonicaliser.Canonicalise(email.Address);
        if (!string.Equals(canonical, email.CanonicalEmail, StringComparison.Ordinal))
        {
            email.SetCanonicalEmail(canonical);
        }
    }

    // Phone — preserved verbatim on Number; canonical (E.164) projected to CanonicalNumber.
    // Same UX rationale as email: a Belgian admin recognises "+32 470 12 34 56" and would
    // be surprised to see it rewritten as "+3247012345678".
    private static void ApplyToPhone(PartyPhone phone)
    {
        string? canonical = PhoneCanonicaliser.Canonicalise(phone.Number);
        if (!string.Equals(canonical, phone.CanonicalNumber, StringComparison.Ordinal))
        {
            phone.SetCanonicalNumber(canonical);
        }
    }
}
