using System.Text.Json;
using Granit.DataProtection;
using Granit.Domain;
using Granit.Mergeable;
using Granit.Mergeable.Domain;
using Granit.Mergeable.Exceptions;
using Granit.Parties.Domain.ValueObjects;
using Granit.Parties.Events;

namespace Granit.Parties.Domain;

/// <summary>
/// Generic party-management aggregate (Party / Tiers / Business Partner). A party represents
/// any natural person, legal entity, or organisational unit that the platform interacts
/// with — customers, suppliers, employees, leads, and combinations thereof.
/// </summary>
/// <remarks>
/// <para><b>Dual-use design.</b> Party is <see cref="IMultiTenant"/>:</para>
/// <list type="bullet">
/// <item><c>TenantId == null</c> ⇒ <i>host-scoped</i> (the SaaS host's contacts: tenants-as-customers, vendors, internal staff).</item>
/// <item><c>TenantId == &lt;tenant&gt;</c> ⇒ <i>tenant-scoped</i> (a tenant's e-commerce / CRM / procurement contacts).</item>
/// </list>
/// <para><b>Multi-role.</b> A single contact can simultaneously hold any combination of
/// <see cref="PartyRoles"/> flags (Customer + Supplier for a reseller you both buy from
/// and sell to, Customer + Employee for staff who consume the product, etc.). Roles are
/// queried via <see cref="HasRole"/> and added/removed individually.</para>
/// <para><b>Hierarchy.</b> A contact may be attached to a parent via <see cref="ParentContactId"/>
/// — typical use cases: a Person belongs to a Company, a Department reports to a parent
/// Company, a subsidiary is owned by a holding. Same-tenant invariant is enforced at attach
/// time. Cycle detection is deferred to a later iteration.</para>
/// <para><b>User linkage.</b> An Individual contact may be linked to an authenticated user
/// via <see cref="UserId"/> — used by self-service portals to surface "my profile". One user
/// may link to at most one contact (uniqueness enforced by the EF configuration).</para>
/// <para><b>Lifecycle.</b> <see cref="PartyStatus.Active"/> ↔ <see cref="PartyStatus.Suspended"/>;
/// either may transition to terminal <see cref="PartyStatus.Archived"/>. Archived
/// contacts are immutable and cannot be reactivated.</para>
/// <para><b>External mappings.</b> Polyglot — at most one mapping per provider, enforced
/// defensively at the aggregate and by a unique index in the EF configuration.</para>
/// </remarks>
public sealed class Party : AuditedAggregateRoot, IMultiTenant, IHasMetadata, IMergeable<Party>
{
    /// <summary>Per-aggregate cap on emails. Bounds reconciliation cost in <c>EfPartyStore.UpdateAsync</c>
    /// and prevents an authenticated <c>Parties.Manage</c> holder from exhausting storage / write throughput
    /// by flooding a single party with addresses (OWASP API4:2023).</summary>
    public const int MaxEmails = 50;

    /// <summary>Per-aggregate cap on phones (see <see cref="MaxEmails"/> rationale).</summary>
    public const int MaxPhones = 50;

    /// <summary>Per-aggregate cap on addresses (see <see cref="MaxEmails"/> rationale).</summary>
    public const int MaxAddresses = 100;

    /// <summary>Per-aggregate cap on external provider mappings. There are at most a few dozen
    /// real-world providers (Stripe, Mollie, Odoo, Sage, NetSuite, …); a higher value indicates
    /// abuse / metric-cardinality probing.</summary>
    public const int MaxExternalMappings = 32;

    private readonly List<PartyExternalMapping> _externalMappings = [];
    private readonly List<PartyAddress> _addresses = [];
    private readonly List<PartyEmail> _emails = [];
    private readonly List<PartyPhone> _phones = [];

    private Party() { }

    /// <summary>Creates a new contact in <see cref="PartyStatus.Active"/> status.</summary>
    /// <param name="id">Unique identifier.</param>
    /// <param name="tenantId">Owning tenant identifier; <c>null</c> for host-scoped contacts.</param>
    /// <param name="kind">Whether the contact is an individual, a company, or a department.</param>
    /// <param name="name">Display / legal name (required, max 256 chars).</param>
    /// <param name="defaultCurrency">ISO 4217 alpha-3 currency code (required, exactly 3 chars).</param>
    /// <param name="roles">Initial role set (defaults to <see cref="PartyRoles.Customer"/>).</param>
    /// <param name="website">Optional website URL.</param>
    /// <param name="language">Optional ISO locale (e.g. <c>"fr-BE"</c>).</param>
    /// <param name="timezone">IANA timezone (defaults to <c>"UTC"</c>).</param>
    /// <param name="taxId">International VAT identifier (e.g. <c>"BE0123456789"</c>).</param>
    /// <param name="registrationNumber">Company registration number (BCE/KBO, SIRET, HRB, …).</param>
    /// <param name="internalNotes">Optional free-form internal notes (admin-only, never exported via Privacy / vCard).</param>
    public static Party Create(
        Guid id,
        Guid? tenantId,
        PartyKind kind,
        string name,
        string defaultCurrency,
        PartyRoles roles = PartyRoles.Customer,
        string? website = null,
        string? language = null,
        string? timezone = null,
        string? taxId = null,
        string? registrationNumber = null,
        string? internalNotes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultCurrency);
        if (defaultCurrency.Length != 3)
        {
            throw new ArgumentException(
                "DefaultCurrency must be a 3-letter ISO 4217 code.", nameof(defaultCurrency));
        }
        if (internalNotes is { Length: > MaxInternalNotesLength })
        {
            throw new ArgumentException(
                $"Internal notes exceed the maximum length of {MaxInternalNotesLength} characters.",
                nameof(internalNotes));
        }

        var contact = new Party
        {
            Id = id,
            TenantId = tenantId,
            Kind = kind,
            Name = name,
            Website = website,
            Language = language,
            Timezone = string.IsNullOrWhiteSpace(timezone) ? "UTC" : timezone,
            DefaultCurrency = defaultCurrency.ToUpperInvariant(),
            TaxId = taxId,
            RegistrationNumber = registrationNumber,
            InternalNotes = string.IsNullOrWhiteSpace(internalNotes) ? null : internalNotes,
            Roles = roles,
            Status = PartyStatus.Active,
        };

        contact.AddDomainEvent(new PartyCreatedEvent(
            PartyId.Create(id), tenantId, kind, name, roles));
        contact.AddDistributedEvent(new PartyCreatedEto(
            PartyId.Create(id), tenantId, kind, name, roles, contact.DefaultCurrency));

        return contact;
    }

    // ── Identity ───────────────────────────────────────────────────

    /// <summary>Whether the contact is an individual, a company, or a department.</summary>
    public PartyKind Kind { get; private set; }

    /// <summary>Display / legal name.</summary>
    [SensitiveData(Level = Sensitivity.Internal)]
    public string Name { get; private set; } = string.Empty;

    /// <summary>Email addresses attached to this contact (multi). Mutated via <see cref="AddEmail"/>, <see cref="RemoveEmail"/>, <see cref="UpdateEmail"/>, <see cref="SetPrimaryEmail"/>.</summary>
    public IReadOnlyList<PartyEmail> Emails => _emails.AsReadOnly();

    /// <summary>Primary email if any (the entry flagged <see cref="PartyEmail.IsPrimary"/>, falling back to the first).</summary>
    public PartyEmail? PrimaryEmail =>
        _emails.FirstOrDefault(e => e.IsPrimary) ?? _emails.FirstOrDefault();

    /// <summary>Phone numbers attached to this contact (multi, typed). Mutated via <see cref="AddPhone"/>, <see cref="RemovePhone"/>, <see cref="UpdatePhone"/>, <see cref="SetPrimaryPhone"/>.</summary>
    public IReadOnlyList<PartyPhone> Phones => _phones.AsReadOnly();

    /// <summary>Primary phone if any.</summary>
    public PartyPhone? PrimaryPhone =>
        _phones.FirstOrDefault(p => p.IsPrimary) ?? _phones.FirstOrDefault();

    /// <summary>Website URL.</summary>
    public string? Website { get; private set; }

    /// <summary>Preferred locale (ISO BCP 47, e.g. <c>"fr-BE"</c>).</summary>
    public string? Language { get; private set; }

    /// <summary>IANA timezone (defaults to <c>"UTC"</c>).</summary>
    public string Timezone { get; private set; } = "UTC";

    /// <summary>ISO 4217 alpha-3 currency code (always upper-case after construction).</summary>
    public string DefaultCurrency { get; private set; } = string.Empty;

    // ── Tax & legal identity (contact-level, not address-level) ───

    /// <summary>International VAT identifier (e.g. <c>"BE0123456789"</c>, <c>"FR12345678901"</c>).</summary>
    [SensitiveData(Level = Sensitivity.Internal)]
    public string? TaxId { get; private set; }

    /// <summary>Company registration number (BCE/KBO, SIRET, HRB, Companies House, …).</summary>
    [SensitiveData(Level = Sensitivity.Internal)]
    public string? RegistrationNumber { get; private set; }

    /// <summary>
    /// Customer-specific tax classification (reverse-charge, exempt, VATIN). Defaults to
    /// <see cref="TaxStatus.Standard"/>. Read by <c>Granit.Tax</c>'s <c>ITaxRateProvider</c>
    /// when a contact is in scope so exempt / reverse-charge customers yield a 0% rate.
    /// Admins opt-in via <see cref="SetTaxStatus"/>.
    /// </summary>
    public TaxStatus TaxStatus { get; private set; } = TaxStatus.Standard;

    /// <summary>
    /// Free-form key/value metadata (Stripe-style <c>customer.metadata</c>). Persisted as
    /// a JSON string column. Use <see cref="MetadataExtensions.GetMetadata"/> +
    /// <see cref="MetadataExtensions.SetMetadataValue"/> for typed read/write of individual
    /// entries, or <see cref="ReplaceMetadata"/> for bulk replacement (admin endpoint).
    /// </summary>
    /// <remarks>
    /// <b>NEVER store PII here.</b> Metadata is surfaced in audit logs, GDPR exports, and the SQL
    /// column itself — keep it to integration sync attributes, segment / tier flags, sales-rep
    /// IDs, and other non-personal categorisation. Personal data belongs on the dedicated
    /// fields (<see cref="Name"/>, <see cref="Emails"/>, <see cref="Phones"/>, etc.) which are
    /// covered by <c>SensitiveData</c> annotations and pseudonymisation.
    /// </remarks>
    [SensitiveData(Level = Sensitivity.Internal)]
    public string? MetadataJson { get; set; }

    /// <summary>Bulk-replaces the metadata dictionary. Pass an empty dictionary to clear.</summary>
    public void ReplaceMetadata(IReadOnlyDictionary<string, string> metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        EnsureMutable();
        MetadataJson = metadata.Count > 0
            ? JsonSerializer.Serialize(metadata)
            : null;
    }

    /// <summary>Per-aggregate cap on internal-notes length (DoS protection).</summary>
    public const int MaxInternalNotesLength = 8_000;

    /// <summary>
    /// Free-form internal notes (admin-only). Multi-line, max 8 000 characters. Mirrors
    /// Stripe <c>customer.description</c> / Odoo <c>res.partner.comment</c>: a place to jot
    /// down operational context (account history, escalation contacts, …) that doesn't fit
    /// the structured fields.
    /// </summary>
    /// <remarks>
    /// <b>NEVER store PII or sensitive data here.</b> Internal notes surface in audit logs
    /// and GDPR exports. Use the dedicated PII fields (<see cref="Name"/>, <see cref="Emails"/>,
    /// …) for personal data — they're covered by <c>SensitiveData</c> annotations and
    /// pseudonymisation.
    /// </remarks>
    [SensitiveData(Level = Sensitivity.Internal)]
    public string? InternalNotes { get; private set; }

    /// <summary>
    /// Tombstone — survivor id when this party has been merged out, <c>null</c> when alive.
    /// Set by the merge orchestrator (<c>EfMergeService&lt;Party&gt;</c>) inside the merge
    /// transaction; never mutated by the aggregate's own domain methods.
    /// </summary>
    /// <remarks>
    /// EF column + index + standard query filter are auto-applied by
    /// <c>ApplyGranitConventions</c> via the <see cref="IHasMergeTombstone"/> contract —
    /// no per-aggregate mapping required.
    /// </remarks>
    public Guid? MergedIntoId { get; private set; }

    /// <summary>Merge timestamp — companion to <see cref="MergedIntoId"/>.</summary>
    public DateTimeOffset? MergedAt { get; private set; }

    /// <summary>Sets or clears the internal-notes free-form text. Pass <c>null</c> or empty to clear.</summary>
    public void SetInternalNotes(string? notes)
    {
        EnsureMutable();
        if (notes is { Length: > MaxInternalNotesLength })
        {
            throw new ArgumentException(
                $"Internal notes exceed the maximum length of {MaxInternalNotesLength} characters.",
                nameof(notes));
        }

        InternalNotes = string.IsNullOrWhiteSpace(notes) ? null : notes;
    }

    /// <summary>
    /// Sets the merge tombstone on this aggregate — called by the merge orchestrator on the
    /// loser at the end of a merge transaction. <see cref="MergedIntoId"/> records the
    /// surviving party id ; <see cref="MergedAt"/> records the merge timestamp. The aggregate
    /// stays in <see cref="PartyStatus.Active"/> so its data remains queryable for audit /
    /// chain-collapse purposes ; the framework-level query filter
    /// (<c>GranitFilterNames.MergeTombstone</c>) hides it from standard listings.
    /// </summary>
    /// <remarks>
    /// Internal mutation point used by <c>PartyMergeableAggregateAdapter.ApplyTombstone</c>.
    /// Skips <see cref="EnsureMutable"/> deliberately — tombstoning is the orchestrator's
    /// final write on the loser even when its lifecycle would normally reject mutations.
    /// </remarks>
    internal void MarkAsMergedInto(Guid survivorId, DateTimeOffset mergedAt)
    {
        if (survivorId == Id)
        {
            throw new InvalidOperationException("A party cannot be merged into itself.");
        }
        MergedIntoId = survivorId;
        MergedAt = mergedAt;
    }

    /// <summary>
    /// Reverses <see cref="MarkAsMergedInto"/> — used by the un-merge endpoint (P3) to revive
    /// a tombstoned loser. Cross-module reference rewriters are NOT replayed by this method ;
    /// the un-merge contract documents that re-routing already-rewritten references is
    /// out of scope (see [TECH DEBT] story #1296).
    /// </summary>
    internal void ClearMergeTombstone()
    {
        MergedIntoId = null;
        MergedAt = null;
    }

    /// <summary>
    /// Updates this aggregate's tombstone pointer — used by chain-collapse during a merge
    /// (<c>A → B</c> followed by <c>B → C</c> rewrites <c>A.MergedIntoId</c> from <c>B</c>
    /// to <c>C</c> so <c>ResolveCurrentAsync</c> needs only one hop).
    /// </summary>
    /// <remarks>
    /// Bulk-applied via SQL <c>ExecuteUpdateAsync</c> by the orchestrator's
    /// <c>CollapseChainTombstonesAsync</c> ; this method exists for in-memory tests + the
    /// single-hop unit-test scenarios.
    /// </remarks>
    internal void RetargetMergeTombstone(Guid newSurvivorId)
    {
        if (MergedIntoId is null)
        {
            throw new InvalidOperationException(
                "Cannot retarget a tombstone on a non-tombstoned party.");
        }
        if (newSurvivorId == Id)
        {
            throw new InvalidOperationException("A party cannot be merged into itself.");
        }
        MergedIntoId = newSurvivorId;
    }

    // ── Merge (IMergeable<Party>) ─────────────────────────────────

    /// <inheritdoc />
    /// <remarks>
    /// Compares <b>scalar fields only</b> ; child collections (Addresses / Emails / Phones /
    /// ExternalMappings) are reconciled by the dedicated <c>PartyChildrenReferenceRewriter</c>
    /// via SQL bulk-update on the shadow FK, never in-memory. See the design notes in the
    /// <see cref="IMergeable{TSelf}"/> contract.
    /// </remarks>
    public IReadOnlyList<FieldConflict> GetConflicts(Party loser)
    {
        ArgumentNullException.ThrowIfNull(loser);

        var conflicts = new List<FieldConflict>();

        AddIfDifferent(conflicts, "Name", Name, loser.Name);
        AddIfDifferent(conflicts, "Website", Website, loser.Website);
        AddIfDifferent(conflicts, "Language", Language, loser.Language);
        AddIfDifferent(conflicts, "Timezone", Timezone, loser.Timezone);
        AddIfDifferent(conflicts, "TaxId", TaxId, loser.TaxId);
        AddIfDifferent(conflicts, "RegistrationNumber", RegistrationNumber, loser.RegistrationNumber);
        AddIfDifferent(conflicts, "AvatarBlobId", AvatarBlobId, loser.AvatarBlobId);

        // ParentContactId — compare the unwrapped Guid? to keep the FieldConflict payload
        // primitive and JSON-friendly (the audit cache stores it).
        AddIfDifferent(conflicts, "ParentContactId",
            ParentContactId?.Value, loser.ParentContactId?.Value);

        // TaxStatus — recommend the non-Standard side when one is Standard ; otherwise
        // SurvivorWins is the safe default and the admin can override at the endpoint.
        if (!Equals(TaxStatus, loser.TaxStatus))
        {
            WinnerSide defaultSide = TaxStatus.Equals(TaxStatus.Standard)
                                      && !loser.TaxStatus.Equals(TaxStatus.Standard)
                ? WinnerSide.Loser
                : WinnerSide.Survivor;
            conflicts.Add(new FieldConflict("TaxStatus", TaxStatus, loser.TaxStatus, defaultSide));
        }

        // UserId — when survivor is null and loser has one, recommend transfer (Loser).
        if (UserId != loser.UserId)
        {
            WinnerSide defaultSide = UserId is null && loser.UserId is not null
                ? WinnerSide.Loser
                : WinnerSide.Survivor;
            conflicts.Add(new FieldConflict("UserId", UserId, loser.UserId, defaultSide));
        }

        // Roles — flags union ; surfaced for admin visibility but never overridable
        // (union is always safe — losing a flag is destructive).
        if (Roles != loser.Roles)
        {
            PartyRoles unionPreview = Roles | loser.Roles;
            conflicts.Add(new FieldConflict("Roles", Roles, loser.Roles, WinnerSide.Survivor)
            {
                // The admin sees both sides ; the actual apply step always unions.
            });
            _ = unionPreview; // documented intent
        }

        return conflicts;
    }

    /// <inheritdoc />
    public void MergeFrom(Party loser, MergeFieldChoices choices)
    {
        ArgumentNullException.ThrowIfNull(loser);
        ArgumentNullException.ThrowIfNull(choices);

        // Hard invariants — non-overridable.
        if (loser.Id == Id)
        {
            throw new MergeException("A party cannot be merged with itself.");
        }
        if (TenantId != loser.TenantId)
        {
            throw new MergeException(
                $"Tenant mismatch — survivor tenant '{TenantId}' vs loser tenant '{loser.TenantId}'.");
        }
        if (Kind != loser.Kind)
        {
            throw new MergeException(
                $"Kind mismatch — survivor '{Kind}' vs loser '{loser.Kind}'.");
        }
        if (!string.Equals(DefaultCurrency, loser.DefaultCurrency, StringComparison.Ordinal))
        {
            throw new MergeException(
                $"Currency mismatch — survivor '{DefaultCurrency}' vs loser '{loser.DefaultCurrency}'.");
        }
        if (Status != PartyStatus.Active)
        {
            throw new MergeException(
                $"Survivor status must be Active — current is '{Status}'.");
        }
        if (loser.Status == PartyStatus.Archived)
        {
            throw new MergeException("Loser is Archived — archived parties cannot be merged out.");
        }

        EnsureMutable();

        // Scalar fields — apply the chosen winner per field.
        Name = ResolveScalar(choices, "Name", Name, loser.Name);
        Website = ResolveScalar(choices, "Website", Website, loser.Website);
        Language = ResolveScalar(choices, "Language", Language, loser.Language);
        Timezone = ResolveScalar(choices, "Timezone", Timezone, loser.Timezone) ?? Timezone;
        TaxId = ResolveScalar(choices, "TaxId", TaxId, loser.TaxId);
        RegistrationNumber = ResolveScalar(choices, "RegistrationNumber", RegistrationNumber, loser.RegistrationNumber);
        AvatarBlobId = ResolveScalar(choices, "AvatarBlobId", AvatarBlobId, loser.AvatarBlobId);

        // ParentContactId — round-trip via PartyId.
        ParentContactId = ResolveParentContactId(choices, ParentContactId, loser.ParentContactId);

        // Roles — always union ; never overridable (losing a flag is destructive).
        Roles |= loser.Roles;

        // TaxStatus — non-trivial default + override.
        WinnerSide taxStatusWinner = choices.ResolveOrDefault("TaxStatus",
            TaxStatus.Equals(TaxStatus.Standard) && !loser.TaxStatus.Equals(TaxStatus.Standard)
                ? WinnerSide.Loser
                : WinnerSide.Survivor);
        if (taxStatusWinner == WinnerSide.Loser)
        {
            TaxStatus = loser.TaxStatus;
        }

        // UserId — transfer the loser's link only if survivor has none and admin doesn't
        // override (or admin explicitly picks Loser).
        WinnerSide userIdWinner = choices.ResolveOrDefault("UserId",
            UserId is null && loser.UserId is not null ? WinnerSide.Loser : WinnerSide.Survivor);
        if (userIdWinner == WinnerSide.Loser)
        {
            UserId = loser.UserId;
        }

        // Metadata — merge dictionaries, survivor wins on key conflict by default ;
        // override per key via choices "Metadata.<key>".
        MergeMetadataInto(loser, choices);

        // InternalNotes — default appends with a separator, override = full replace.
        WinnerSide notesWinner = choices.ResolveOrDefault("InternalNotes", WinnerSide.Survivor);
        InternalNotes = ApplyInternalNotesMerge(InternalNotes, loser.InternalNotes, loser.Id, notesWinner);

        RaiseUpdated();
    }

    private static void AddIfDifferent<T>(
        List<FieldConflict> conflicts,
        string fieldPath,
        T survivor,
        T loser)
    {
        if (!EqualityComparer<T>.Default.Equals(survivor, loser))
        {
            conflicts.Add(new FieldConflict(fieldPath, survivor, loser, WinnerSide.Survivor));
        }
    }

    private static T ResolveScalar<T>(MergeFieldChoices choices, string fieldPath, T survivor, T loser) =>
        choices.ResolveOrDefault(fieldPath, WinnerSide.Survivor) == WinnerSide.Loser
            ? loser
            : survivor;

    private static PartyId? ResolveParentContactId(
        MergeFieldChoices choices, PartyId? survivor, PartyId? loser) =>
        choices.ResolveOrDefault("ParentContactId", WinnerSide.Survivor) == WinnerSide.Loser
            ? loser
            : survivor;

    private void MergeMetadataInto(Party loser, MergeFieldChoices choices)
    {
        IReadOnlyDictionary<string, string> survivorMeta = this.GetMetadata();
        IReadOnlyDictionary<string, string> loserMeta = loser.GetMetadata();
        if (survivorMeta.Count == 0 && loserMeta.Count == 0)
        {
            return;
        }

        var merged = new Dictionary<string, string>(survivorMeta, StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> kv in loserMeta)
        {
            string fieldPath = $"Metadata.{kv.Key}";
            bool inSurvivor = merged.ContainsKey(kv.Key);
            WinnerSide winner = choices.ResolveOrDefault(fieldPath,
                inSurvivor ? WinnerSide.Survivor : WinnerSide.Loser);
            if (!inSurvivor || winner == WinnerSide.Loser)
            {
                merged[kv.Key] = kv.Value;
            }
        }
        this.ReplaceMetadata(merged);
    }

    private static string? ApplyInternalNotesMerge(
        string? survivorNotes,
        string? loserNotes,
        Guid loserId,
        WinnerSide winner)
    {
        if (string.IsNullOrWhiteSpace(loserNotes))
        {
            return survivorNotes;
        }

        if (winner == WinnerSide.Loser)
        {
            return loserNotes;
        }

        // Default append with a separator that mentions the loser id for traceability.
        string separator = $"\n\n--- merged from {loserId:N} ---\n";
        return string.IsNullOrWhiteSpace(survivorNotes)
            ? loserNotes
            : survivorNotes + separator + loserNotes;
    }

    // ── Addresses ─────────────────────────────────────────────────

    /// <summary>
    /// All postal addresses attached to this contact. A contact may carry several
    /// (billing, shipping, other) — at most one default per <see cref="AddressKind"/>.
    /// Mutated via <see cref="AddAddress"/>, <see cref="RemoveAddress"/>,
    /// <see cref="SetDefaultAddress"/>, <see cref="UpdateAddress"/>.
    /// </summary>
    public IReadOnlyList<PartyAddress> Addresses => _addresses.AsReadOnly();

    /// <summary>The default billing address, if any. Convenience accessor for downstream modules.</summary>
    public PartyAddress? DefaultBillingAddress =>
        _addresses.FirstOrDefault(a => a.Kind == AddressKind.Billing && a.IsDefault)
        ?? _addresses.FirstOrDefault(a => a.Kind == AddressKind.Billing);

    /// <summary>The default shipping address, if any.</summary>
    public PartyAddress? DefaultShippingAddress =>
        _addresses.FirstOrDefault(a => a.Kind == AddressKind.Shipping && a.IsDefault)
        ?? _addresses.FirstOrDefault(a => a.Kind == AddressKind.Shipping);

    // ── Avatar ────────────────────────────────────────────────────

    /// <summary>
    /// Soft reference to a blob in <c>Granit.BlobStorage</c> holding the contact's
    /// avatar — photo for an Individual, logo for a Company. <c>null</c> = no avatar.
    /// Set/clear via <see cref="SetAvatar"/> / <see cref="ClearAvatar"/>.
    /// </summary>
    public Guid? AvatarBlobId { get; private set; }

    // ── Hierarchy ─────────────────────────────────────────────────

    /// <summary>Parent contact identifier (Person→Company, subsidiary→holding, …). Same-tenant only.</summary>
    public PartyId? ParentContactId { get; private set; }

    // ── User linkage ──────────────────────────────────────────────

    /// <summary>
    /// Authenticated user identifier this contact represents. Only meaningful for
    /// <see cref="PartyKind.Individual"/>. One user → at most one contact (enforced by
    /// a partial unique index in the EF configuration).
    /// </summary>
    public Guid? UserId { get; private set; }

    // ── Roles ─────────────────────────────────────────────────────

    /// <summary>Set of roles this contact plays (<see cref="PartyRoles"/> flags).</summary>
    public PartyRoles Roles { get; private set; }

    // ── Lifecycle ─────────────────────────────────────────────────

    /// <summary>Lifecycle status.</summary>
    public PartyStatus Status { get; private set; }

    // ── External mappings ─────────────────────────────────────────

    /// <summary>External provider mappings (Stripe customer ID, Odoo partner ID, …).</summary>
    public IReadOnlyList<PartyExternalMapping> ExternalMappings => _externalMappings.AsReadOnly();

    // ── Multi-tenancy ─────────────────────────────────────────────

    /// <inheritdoc/>
    public Guid? TenantId { get; private set; }

    /// <summary>Explicit interface implementation: lets the audit/multi-tenant interceptor
    /// assign the tenant during materialisation while keeping public access read-only.</summary>
    Guid? IMultiTenant.TenantId { get => TenantId; set => TenantId = value; }

    // ─────────────────────────────────────────────────────────────────
    // Lifecycle (idempotent transitions)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Reactivates a suspended contact. Idempotent; throws on Archived.</summary>
    public bool Activate()
    {
        if (Status == PartyStatus.Active) { return false; }
        EnsureNotArchived(nameof(Activate));

        Status = PartyStatus.Active;
        AddDomainEvent(new PartyActivatedEvent(PartyId.Create(Id), TenantId));
        AddDistributedEvent(new PartyActivatedEto(PartyId.Create(Id), TenantId));
        return true;
    }

    /// <summary>Suspends an active contact. Idempotent; throws on Archived.</summary>
    public bool Suspend(string? reason = null)
    {
        if (Status == PartyStatus.Suspended) { return false; }
        EnsureNotArchived(nameof(Suspend));

        Status = PartyStatus.Suspended;
        AddDomainEvent(new PartySuspendedEvent(PartyId.Create(Id), TenantId, reason));
        AddDistributedEvent(new PartySuspendedEto(PartyId.Create(Id), TenantId, reason));
        return true;
    }

    /// <summary>Archives the contact (terminal state). Idempotent.</summary>
    public bool Archive()
    {
        if (Status == PartyStatus.Archived) { return false; }

        Status = PartyStatus.Archived;
        AddDomainEvent(new PartyArchivedEvent(PartyId.Create(Id), TenantId));
        AddDistributedEvent(new PartyArchivedEto(PartyId.Create(Id), TenantId));
        return true;
    }

    /// <summary>
    /// Pseudonymises every PII field on the contact while preserving the row for accounting
    /// integrity (ISO 27001 / legal retention). Replaces <see cref="Name"/> with the supplied
    /// placeholder, clears all <see cref="Emails"/>, <see cref="Phones"/>, <see cref="Addresses"/>,
    /// and unlinks the user. Tax and registration identifiers and external mappings are kept
    /// because downstream finance systems still reference them. Bypasses the standard mutability
    /// guard — GDPR Article 17 erasure must succeed even on an Archived contact.
    /// </summary>
    /// <returns><c>true</c> if any PII was actually cleared; <c>false</c> when nothing remained
    /// to pseudonymise (idempotent re-run).</returns>
    public bool PseudonymizePersonalData(string placeholder = "[deleted]")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(placeholder);

        bool changed = false;
        if (!string.Equals(Name, placeholder, StringComparison.Ordinal))
        {
            Name = placeholder;
            changed = true;
        }
        if (_emails.Count > 0) { _emails.Clear(); changed = true; }
        if (_phones.Count > 0) { _phones.Clear(); changed = true; }
        if (_addresses.Count > 0) { _addresses.Clear(); changed = true; }
        if (Website is not null) { Website = null; changed = true; }
        if (UserId is not null) { UserId = null; changed = true; }

        if (changed)
        {
            AddDomainEvent(new PartyPersonalDataPseudonymizedEvent(PartyId.Create(Id), TenantId));
            AddDistributedEvent(new PartyPersonalDataPseudonymizedEto(PartyId.Create(Id), TenantId));
        }
        return changed;
    }

    // ─────────────────────────────────────────────────────────────────
    // Identity & address updates
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Updates the contact's identity fields (name, website, locale, timezone). Emails
    /// and phones live in their own collections — see <see cref="AddEmail"/>,
    /// <see cref="UpdateEmail"/>, <see cref="AddPhone"/>, <see cref="UpdatePhone"/>.
    /// </summary>
    public void UpdateIdentity(
        string name,
        string? website = null,
        string? language = null,
        string? timezone = null,
        string? internalNotes = null)
    {
        EnsureMutable();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (internalNotes is { Length: > MaxInternalNotesLength })
        {
            throw new ArgumentException(
                $"Internal notes exceed the maximum length of {MaxInternalNotesLength} characters.",
                nameof(internalNotes));
        }

        Name = name;
        Website = website;
        Language = language;
        Timezone = string.IsNullOrWhiteSpace(timezone) ? Timezone : timezone;
        InternalNotes = string.IsNullOrWhiteSpace(internalNotes) ? null : internalNotes;

        RaiseUpdated();
    }

    /// <summary>
    /// Returns the canonical billing-address snapshot for this contact: combines the default
    /// <see cref="AddressKind.Billing"/> entry from <see cref="Addresses"/> with the
    /// contact-level <see cref="TaxId"/> (VAT) and falls back to <see cref="Name"/> as the
    /// company name when the address has none. <c>null</c> when no default billing address
    /// is registered. Used by <c>Granit.Invoicing.Domain.Invoice.Finalize</c> to capture the
    /// legal address shown on the issued document.
    /// </summary>
    public BillingAddress? GetBillingAddressSnapshot()
    {
        PartyAddress? defaultBilling = DefaultBillingAddress;
        if (defaultBilling is null)
        {
            return null;
        }

        Address address = defaultBilling.Value;
        return BillingAddress.Create(
            line1: address.Line1,
            city: address.City,
            postalCode: address.PostalCode,
            country: address.Country,
            companyName: address.CompanyName ?? Name,
            line2: address.Line2,
            state: address.State,
            vatNumber: TaxId);
    }

    /// <summary>
    /// Sets or replaces the customer-specific <see cref="TaxStatus"/>. Idempotent for equal
    /// values. Pass <c>null</c> or <see cref="TaxStatus.Standard"/> to reset to the default
    /// (no special status — country / standard rate applies).
    /// </summary>
    /// <returns><c>true</c> when the status changed; <c>false</c> when the value was already equal.</returns>
    public bool SetTaxStatus(TaxStatus? taxStatus)
    {
        EnsureMutable();
        TaxStatus next = taxStatus ?? TaxStatus.Standard;

        if (Equals(TaxStatus, next))
        {
            return false;
        }

        TaxStatus previous = TaxStatus;
        TaxStatus = next;
        AddDomainEvent(new PartyTaxStatusChangedEvent(
            PartyId.Create(Id), TenantId, previous, next));
        RaiseUpdated();
        return true;
    }

    // ─────────────────────────────────────────────────────────────────
    // Emails (multi, with primary)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a new email address. When <paramref name="isPrimary"/> is true, the previously
    /// primary email (if any) is demoted. The first email added is auto-promoted to primary
    /// regardless of caller intent.
    /// </summary>
    public void AddEmail(Guid emailId, string address, bool isPrimary = false, string? label = null)
    {
        EnsureMutable();
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        EnsureCapacity(_emails.Count, MaxEmails, nameof(Emails));

        if (isPrimary)
        {
            ClearPrimaryEmail();
        }

        bool effectivePrimary = isPrimary || _emails.Count == 0;

        _emails.Add(PartyEmail.Create(emailId, address, effectivePrimary, label));
        RaiseUpdated();
    }

    /// <summary>Removes the email with the given id. Idempotent. Promotes another to primary if needed.</summary>
    public bool RemoveEmail(Guid emailId)
    {
        EnsureMutable();
        PartyEmail? existing = _emails.FirstOrDefault(e => e.Id == emailId);
        if (existing is null) { return false; }

        _emails.Remove(existing);

        if (existing.IsPrimary)
        {
            _emails.FirstOrDefault()?.MarkPrimary(true);
        }

        RaiseUpdated();
        return true;
    }

    /// <summary>Replaces the address (and optional label) of an existing email entry.</summary>
    public void UpdateEmail(Guid emailId, string address, string? label = null)
    {
        EnsureMutable();
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        PartyEmail existing = _emails.FirstOrDefault(e => e.Id == emailId)
            ?? throw new InvalidOperationException(
                $"Party '{Id}' has no email with id '{emailId}'.");

        existing.Replace(address, label);
        RaiseUpdated();
    }

    /// <summary>Marks the given email as primary. Idempotent.</summary>
    public void SetPrimaryEmail(Guid emailId)
    {
        EnsureMutable();
        PartyEmail target = _emails.FirstOrDefault(e => e.Id == emailId)
            ?? throw new InvalidOperationException(
                $"Party '{Id}' has no email with id '{emailId}'.");

        if (target.IsPrimary) { return; }

        ClearPrimaryEmail();
        target.MarkPrimary(true);
        RaiseUpdated();
    }

    private void ClearPrimaryEmail()
    {
        foreach (PartyEmail email in _emails.Where(e => e.IsPrimary))
        {
            email.MarkPrimary(false);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Phones (multi, typed, with primary)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a new phone number. When <paramref name="isPrimary"/> is true, the previously
    /// primary phone (if any) is demoted. The first phone added is auto-promoted to primary.
    /// </summary>
    public void AddPhone(
        Guid phoneId,
        PhoneKind kind,
        string number,
        bool isPrimary = false,
        string? label = null)
    {
        EnsureMutable();
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        EnsureCapacity(_phones.Count, MaxPhones, nameof(Phones));

        if (isPrimary)
        {
            ClearPrimaryPhone();
        }

        bool effectivePrimary = isPrimary || _phones.Count == 0;

        _phones.Add(PartyPhone.Create(phoneId, kind, number, effectivePrimary, label));
        RaiseUpdated();
    }

    /// <summary>Removes the phone with the given id. Idempotent.</summary>
    public bool RemovePhone(Guid phoneId)
    {
        EnsureMutable();
        PartyPhone? existing = _phones.FirstOrDefault(p => p.Id == phoneId);
        if (existing is null) { return false; }

        _phones.Remove(existing);

        if (existing.IsPrimary)
        {
            _phones.FirstOrDefault()?.MarkPrimary(true);
        }

        RaiseUpdated();
        return true;
    }

    /// <summary>Replaces the kind / number / label of an existing phone entry.</summary>
    public void UpdatePhone(Guid phoneId, PhoneKind kind, string number, string? label = null)
    {
        EnsureMutable();
        ArgumentException.ThrowIfNullOrWhiteSpace(number);

        PartyPhone existing = _phones.FirstOrDefault(p => p.Id == phoneId)
            ?? throw new InvalidOperationException(
                $"Party '{Id}' has no phone with id '{phoneId}'.");

        existing.Replace(kind, number, label);
        RaiseUpdated();
    }

    /// <summary>Marks the given phone as primary. Idempotent.</summary>
    public void SetPrimaryPhone(Guid phoneId)
    {
        EnsureMutable();
        PartyPhone target = _phones.FirstOrDefault(p => p.Id == phoneId)
            ?? throw new InvalidOperationException(
                $"Party '{Id}' has no phone with id '{phoneId}'.");

        if (target.IsPrimary) { return; }

        ClearPrimaryPhone();
        target.MarkPrimary(true);
        RaiseUpdated();
    }

    private void ClearPrimaryPhone()
    {
        foreach (PartyPhone phone in _phones.Where(p => p.IsPrimary))
        {
            phone.MarkPrimary(false);
        }
    }

    /// <summary>
    /// Adds a new typed address. When <paramref name="isDefault"/> is true, any other
    /// address of the same kind is unmarked as default first.
    /// </summary>
    public void AddAddress(
        Guid addressId,
        AddressKind kind,
        Address value,
        bool isDefault = false,
        string? label = null)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(value);
        EnsureCapacity(_addresses.Count, MaxAddresses, nameof(Addresses));

        if (isDefault)
        {
            ClearDefault(kind);
        }

        // Promote the first-of-kind address to default if none exists, regardless of caller intent.
        bool effectiveDefault = isDefault || !_addresses.Any(a => a.Kind == kind);

        _addresses.Add(PartyAddress.Create(addressId, kind, value, effectiveDefault, label));
        RaiseUpdated();
    }

    /// <summary>Removes the address with the given id. Idempotent.</summary>
    public bool RemoveAddress(Guid addressId)
    {
        EnsureMutable();
        PartyAddress? existing = _addresses.FirstOrDefault(a => a.Id == addressId);
        if (existing is null) { return false; }

        _addresses.Remove(existing);

        // Promote a remaining address of the same kind to default if we just removed the default.
        if (existing.IsDefault)
        {
            PartyAddress? next = _addresses.FirstOrDefault(a => a.Kind == existing.Kind);
            next?.MarkDefault(true);
        }

        RaiseUpdated();
        return true;
    }

    /// <summary>Replaces the address payload (and optional label) of an existing entry.</summary>
    public void UpdateAddress(Guid addressId, Address value, string? label = null)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(value);

        PartyAddress existing = _addresses.FirstOrDefault(a => a.Id == addressId)
            ?? throw new InvalidOperationException(
                $"Party '{Id}' has no address with id '{addressId}'.");

        existing.Replace(value, label);
        RaiseUpdated();
    }

    /// <summary>Marks the given address as the default for its <see cref="AddressKind"/>.</summary>
    public void SetDefaultAddress(Guid addressId)
    {
        EnsureMutable();
        PartyAddress target = _addresses.FirstOrDefault(a => a.Id == addressId)
            ?? throw new InvalidOperationException(
                $"Party '{Id}' has no address with id '{addressId}'.");

        if (target.IsDefault) { return; }

        ClearDefault(target.Kind);
        target.MarkDefault(true);
        RaiseUpdated();
    }

    private void ClearDefault(AddressKind kind)
    {
        foreach (PartyAddress addr in _addresses.Where(a => a.Kind == kind && a.IsDefault))
        {
            addr.MarkDefault(false);
        }
    }

    /// <summary>Updates the tax / legal identity fields.</summary>
    public void UpdateTaxIdentity(string? taxId, string? registrationNumber)
    {
        EnsureMutable();
        TaxId = taxId;
        RegistrationNumber = registrationNumber;
        RaiseUpdated();
    }

    // ─────────────────────────────────────────────────────────────────
    // Hierarchy
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Attaches this contact to a parent contact.</summary>
    /// <param name="parentContactId">The parent contact's identifier.</param>
    /// <param name="parentTenantId">The parent contact's <see cref="IMultiTenant.TenantId"/> for same-scope validation.</param>
    public void AttachToParent(PartyId parentContactId, Guid? parentTenantId)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(parentContactId);

        if (parentContactId.Value == Id)
        {
            throw new InvalidOperationException("A contact cannot be its own parent.");
        }

        if (parentTenantId != TenantId)
        {
            throw new InvalidOperationException(
                "Parent contact must live in the same tenant scope (host or specific tenant).");
        }

        ParentContactId = parentContactId;
        AddDomainEvent(new PartyAttachedToParentEvent(
            PartyId.Create(Id), TenantId, parentContactId));
    }

    /// <summary>Detaches this contact from its parent. Idempotent.</summary>
    public bool DetachFromParent()
    {
        EnsureMutable();
        if (ParentContactId is null) { return false; }

        PartyId former = ParentContactId;
        ParentContactId = null;
        AddDomainEvent(new PartyDetachedFromParentEvent(
            PartyId.Create(Id), TenantId, former));
        return true;
    }

    // ─────────────────────────────────────────────────────────────────
    // User linkage
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Links this contact to an authenticated user. Only valid for
    /// <see cref="PartyKind.Individual"/> contacts.
    /// </summary>
    public void LinkToUser(Guid userId)
    {
        EnsureMutable();
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User identifier must not be empty.", nameof(userId));
        }
        if (Kind != PartyKind.Individual)
        {
            throw new InvalidOperationException(
                $"Only Individual contacts can be linked to a user (this contact is {Kind}).");
        }

        UserId = userId;
        AddDomainEvent(new PartyLinkedToUserEvent(
            PartyId.Create(Id), TenantId, userId));
    }

    /// <summary>Clears the user linkage. Idempotent.</summary>
    public bool UnlinkFromUser()
    {
        EnsureMutable();
        if (UserId is null) { return false; }
        UserId = null;
        return true;
    }

    // ─────────────────────────────────────────────────────────────────
    // Avatar
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets or replaces the avatar (photo for an Individual, logo for a Company).
    /// The blob itself lives in <c>Granit.BlobStorage</c>; this method stores only
    /// the soft reference.
    /// </summary>
    public void SetAvatar(Guid blobId)
    {
        EnsureMutable();
        if (blobId == Guid.Empty)
        {
            throw new ArgumentException("Blob identifier must not be empty.", nameof(blobId));
        }
        AvatarBlobId = blobId;
        RaiseUpdated();
    }

    /// <summary>Clears the avatar reference. Idempotent.</summary>
    public bool ClearAvatar()
    {
        EnsureMutable();
        if (AvatarBlobId is null) { return false; }
        AvatarBlobId = null;
        RaiseUpdated();
        return true;
    }

    // ─────────────────────────────────────────────────────────────────
    // Roles
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Adds a role flag. Idempotent.</summary>
    public bool AddRole(PartyRoles role)
    {
        EnsureMutable();
        if (role == PartyRoles.None) { return false; }
        if ((Roles & role) == role) { return false; }

        Roles |= role;
        AddDomainEvent(new PartyRoleAddedEvent(PartyId.Create(Id), TenantId, role));
        return true;
    }

    /// <summary>Removes a role flag. Idempotent.</summary>
    public bool RemoveRole(PartyRoles role)
    {
        EnsureMutable();
        if (role == PartyRoles.None) { return false; }
        if ((Roles & role) == 0) { return false; }

        Roles &= ~role;
        AddDomainEvent(new PartyRoleRemovedEvent(PartyId.Create(Id), TenantId, role));
        return true;
    }

    /// <summary>Returns <c>true</c> if all flags in <paramref name="role"/> are set.</summary>
    public bool HasRole(PartyRoles role) => (Roles & role) == role;

    // ─────────────────────────────────────────────────────────────────
    // External mappings
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Registers an external provider identifier against this contact.</summary>
    /// <exception cref="InvalidOperationException">A mapping for <paramref name="providerName"/> already exists.</exception>
    public void AddExternalMapping(Guid mappingId, string providerName, string externalId)
    {
        EnsureMutable();
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        EnsureCapacity(_externalMappings.Count, MaxExternalMappings, nameof(ExternalMappings));

        if (_externalMappings.Any(m =>
            string.Equals(m.ProviderName, providerName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Party '{Id}' already has an external mapping for provider '{providerName}'. "
                + "Remove the existing one before registering a new identifier.");
        }

        _externalMappings.Add(PartyExternalMapping.Create(mappingId, providerName, externalId));

        AddDomainEvent(new PartyExternalMappingAddedEvent(
            PartyId.Create(Id), TenantId, providerName, externalId));
        AddDistributedEvent(new PartyExternalMappingAddedEto(
            PartyId.Create(Id), TenantId, providerName, externalId));
    }

    /// <summary>Removes the mapping for the given provider, if any.</summary>
    public bool RemoveExternalMapping(string providerName)
    {
        EnsureMutable();
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        PartyExternalMapping? existing = _externalMappings.FirstOrDefault(m =>
            string.Equals(m.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));

        if (existing is null) { return false; }

        _externalMappings.Remove(existing);
        return true;
    }

    /// <summary>Looks up the external identifier for a given provider, if registered.</summary>
    public string? FindExternalId(string providerName) =>
        _externalMappings.FirstOrDefault(m =>
            string.Equals(m.ProviderName, providerName, StringComparison.OrdinalIgnoreCase))?
            .ExternalId;

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private void EnsureMutable()
    {
        if (Status == PartyStatus.Archived)
        {
            throw new InvalidOperationException(
                $"Party '{Id}' is Archived. Archived contacts are immutable.");
        }
    }

    private void EnsureNotArchived(string operation)
    {
        if (Status == PartyStatus.Archived)
        {
            throw new InvalidOperationException(
                $"Party '{Id}' is Archived. {operation}() is not allowed.");
        }
    }

    private void EnsureCapacity(int currentCount, int max, string collectionName)
    {
        if (currentCount >= max)
        {
            throw new InvalidOperationException(
                $"Party '{Id}' has reached the maximum number of {collectionName} ({max}). "
                + "Remove an existing entry before adding a new one.");
        }
    }

    private void RaiseUpdated()
    {
        AddDomainEvent(new PartyUpdatedEvent(PartyId.Create(Id), TenantId));
        AddDistributedEvent(new PartyUpdatedEto(PartyId.Create(Id), TenantId));
    }
}
