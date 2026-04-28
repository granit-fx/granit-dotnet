using Granit.Parties.Domain;

namespace Granit.Parties.Deduplication.Domain;

/// <summary>
/// Minimal admin-supplied snapshot of a party being created — fed to
/// <see cref="IPartyDuplicateDetector.FindCandidatesAsync"/> at create time so the wizard
/// can warn ("3 potential duplicates") <em>before</em> the row is inserted. Flat,
/// serialisable, no domain invariants — the caller has not yet built a <c>Party</c>
/// aggregate at the point this struct is produced.
/// </summary>
/// <param name="TenantId">Owning tenant; <c>null</c> for host-scoped parties. Detection is
/// always tenant-scoped (cross-tenant duplicates are not a thing).</param>
/// <param name="Kind">Whether the party is an individual, a company, or a department —
/// drives the choice between the two configurable similarity thresholds (Name vs Company).</param>
/// <param name="Name">Display / legal name. Required.</param>
/// <param name="TaxId">VAT or registration number. Optional; canonicalised before lookup.</param>
/// <param name="Emails">Free-form email strings. Each is canonicalised before Tier-1 lookup.</param>
/// <param name="Phones">Free-form phone strings. Each is canonicalised before Tier-1 lookup.</param>
/// <param name="AddressLine1">Optional first line of an address — used by Tier-3 normalised
/// Levenshtein scoring.</param>
/// <param name="PostalCode">Optional postal code — used by Tier-3 to disambiguate two
/// people sharing a name in different cities.</param>
public sealed record PartyDraft(
    Guid? TenantId,
    PartyKind Kind,
    string Name,
    string? TaxId = null,
    IReadOnlyList<string>? Emails = null,
    IReadOnlyList<string>? Phones = null,
    string? AddressLine1 = null,
    string? PostalCode = null);
