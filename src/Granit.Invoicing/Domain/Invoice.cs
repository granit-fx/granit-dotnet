using Granit.Domain;
using Granit.Invoicing.Domain.ValueObjects;
using Granit.Invoicing.Events;
using Granit.Parties.Domain.ValueObjects;
using Granit.Workflow.Domain;

namespace Granit.Invoicing.Domain;

/// <summary>
/// A financial document (invoice or credit note) representing a monetary obligation.
/// </summary>
/// <remarks>
/// <para>
/// Invoices and credit notes share the same aggregate — same concept, different sign.
/// This follows the accounting pattern used by Odoo (<c>account.move</c>) and Stripe.
/// The <see cref="DocumentType"/> field distinguishes them.
/// </para>
/// <para>
/// Credit notes reference a parent invoice via <see cref="ParentInvoiceId"/>
/// and use separate number sequences (CN-2026-0001 vs INV-2026-0001).
/// The cumulative total of all credit notes for an invoice MUST NOT exceed
/// the parent invoice's Total.
/// </para>
/// <para>
/// Financial fields are <b>immutable after finalization</b> (status leaves Draft).
/// <see cref="InvoiceLineItem.SourceType"/> enables agnosticism across modules.
/// </para>
/// </remarks>
public sealed class Invoice : AuditedAggregateRoot, IWorkflowStateful, IMultiTenant
{
    private readonly List<InvoiceLineItem> _lineItems = [];
    private readonly List<InvoiceExternalReference> _externalReferences = [];
    private readonly List<InvoiceDocument> _documents = [];

    private Invoice() { }

    /// <summary>Creates a new invoice in Draft status.</summary>
    /// <param name="id">Unique invoice identifier.</param>
    /// <param name="tenantId">Owning tenant identifier (multi-tenant isolation, orthogonal to <paramref name="partyId"/>).</param>
    /// <param name="partyId">Identifier of the <c>Granit.Parties.Party</c> that holds the billing identity for this invoice. Required.</param>
    /// <param name="documentType">Whether this is an invoice or a credit note.</param>
    /// <param name="currency">ISO 4217 currency code.</param>
    /// <param name="collectionMethod">How payment is collected (auto-charge or manual).</param>
    /// <param name="billingReason">Why this document was created.</param>
    /// <param name="creditNoteInfo">Parent invoice reference and reason (required for credit notes).</param>
    /// <param name="period">Billing period covered by this invoice.</param>
    public static Invoice Create(
        Guid id,
        Guid tenantId,
        PartyId partyId,
        InvoiceDocumentType documentType,
        string currency,
        CollectionMethod collectionMethod,
        BillingReason billingReason,
        CreditNoteInfo? creditNoteInfo = null,
        BillingPeriod? period = null)
    {
        ArgumentNullException.ThrowIfNull(partyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        if (documentType == InvoiceDocumentType.CreditNote && creditNoteInfo is null)
        {
            throw new ArgumentException(
                "Credit notes must reference a parent invoice.", nameof(creditNoteInfo));
        }

        var invoice = new Invoice
        {
            Id = id,
            TenantId = tenantId,
            PartyId = partyId,
            DocumentType = documentType,
            Currency = currency,
            CollectionMethod = collectionMethod,
            BillingReason = billingReason,
            ParentInvoiceId = creditNoteInfo?.ParentInvoiceId,
            CreditNoteReason = creditNoteInfo?.Reason,
            Status = InvoiceStatus.Draft,
            PeriodStart = period?.Start,
            PeriodEnd = period?.End,
        };

        invoice.AddDomainEvent(new InvoiceCreatedEvent(id, tenantId, partyId.Value));
        return invoice;
    }

    /// <summary>Convenience factory for creating a credit note linked to a parent invoice.</summary>
    public static Invoice CreateCreditNote(
        Guid id,
        Guid tenantId,
        PartyId partyId,
        InvoiceId parentInvoiceId,
        string currency,
        string reason) =>
        Create(
            id, tenantId, partyId, InvoiceDocumentType.CreditNote, currency,
            CollectionMethod.Auto, BillingReason.Manual,
            creditNoteInfo: new CreditNoteInfo(parentInvoiceId, reason));

    // ── Properties ─────────────────────────────────────────────────────

    /// <summary>
    /// Identifier of the <c>Granit.Parties.Party</c> aggregate that holds the billing
    /// identity (legal name, addresses, external mappings) for this invoice. Required —
    /// the invoice is meaningless without a billing party. Use
    /// <c>IDefaultPartyResolver.GetDefaultForTenantAsync</c> to obtain the host-scoped
    /// party representing a tenant when migrating from tenant-keyed billing.
    /// </summary>
    public PartyId PartyId { get; private set; } = null!;

    /// <summary>Whether this is an invoice or a credit note.</summary>
    public InvoiceDocumentType DocumentType { get; private set; }

    /// <summary>Sequential document number (assigned on finalization, gap-free).</summary>
    public string? InvoiceNumber { get; private set; }

    /// <summary>Current lifecycle status.</summary>
    public InvoiceStatus Status { get; private set; }

    /// <summary>How payment is collected.</summary>
    public CollectionMethod CollectionMethod { get; private set; }

    /// <summary>Why this document was created.</summary>
    public BillingReason BillingReason { get; private set; }

    /// <summary>ISO 4217 currency code.</summary>
    public string Currency { get; private set; } = string.Empty;

    /// <summary>
    /// Immutable snapshot of the party's billing address taken at <see cref="Finalize"/>
    /// time. <c>null</c> while the invoice is still in <see cref="InvoiceStatus.Draft"/>;
    /// callers in Draft state should read the live address from
    /// <c>Granit.Parties.Domain.Party.BillingAddress</c> via <see cref="PartyId"/>.
    /// Once an invoice is finalized this snapshot must remain stable forever — the address
    /// shown on a legal invoice cannot change retroactively even if the party later
    /// updates their billing address.
    /// </summary>
    public Granit.Parties.Domain.BillingAddress? IssuedBillingAddressSnapshot { get; private set; }

    /// <summary>Parent invoice ID (set for credit notes, null for invoices).</summary>
    public InvoiceId? ParentInvoiceId { get; private set; }

    /// <summary>Reason for the credit note (null for invoices).</summary>
    public string? CreditNoteReason { get; private set; }

    /// <summary>Sum of line item amounts before tax.</summary>
    public decimal Subtotal { get; private set; }

    /// <summary>Total tax (set from ITaxCalculator, authoritative for rounding).</summary>
    public decimal TaxTotal { get; private set; }

    /// <summary>Total amount. Positive for invoices, negative for credit notes.</summary>
    public decimal Total { get; private set; }

    /// <summary>Cumulative payments received.</summary>
    public decimal AmountPaid { get; private set; }

    /// <summary>Cumulative credit notes applied.</summary>
    public decimal AmountCredited { get; private set; }

    /// <summary>Remaining amount due: max(0, Total - AmountPaid - AmountCredited).</summary>
    public decimal AmountRemaining { get; private set; }

    /// <summary>Overpayment amount: max(0, AmountPaid + AmountCredited - Total).</summary>
    public decimal Overpayment { get; private set; }

    /// <summary>When the document was finalized.</summary>
    public DateTimeOffset? IssuedAt { get; private set; }

    /// <summary>Payment deadline (invoices only).</summary>
    public DateTimeOffset? DueAt { get; private set; }

    /// <summary>When full payment was received (invoices only).</summary>
    public DateTimeOffset? PaidAt { get; private set; }

    /// <summary>Billing period start.</summary>
    public DateTimeOffset? PeriodStart { get; private set; }

    /// <summary>Billing period end.</summary>
    public DateTimeOffset? PeriodEnd { get; private set; }

    /// <summary>Line items.</summary>
    public IReadOnlyList<InvoiceLineItem> LineItems => _lineItems.AsReadOnly();

    /// <summary>External provider references (Stripe, Odoo).</summary>
    public IReadOnlyList<InvoiceExternalReference> ExternalReferences => _externalReferences.AsReadOnly();

    /// <summary>Generated documents (PDFs).</summary>
    public IReadOnlyList<InvoiceDocument> Documents => _documents.AsReadOnly();

    /// <inheritdoc />
    public Guid? TenantId { get; private set; }

    /// <inheritdoc />
    Guid? IMultiTenant.TenantId { get; set; }

    // ── Computed ───────────────────────────────────────────────────────

    /// <summary>Whether this document is a credit note.</summary>
    public bool IsCreditNote => DocumentType == InvoiceDocumentType.CreditNote;

    /// <summary>Whether this invoice is overdue (Open + past DueAt).</summary>
    public bool IsOverdue(DateTimeOffset now) =>
        Status == InvoiceStatus.Open && DueAt.HasValue && DueAt.Value < now;

    // ── IWorkflowStateful ──────────────────────────────────────────────

    static string IWorkflowStateful.StatusPropertyName => nameof(Status);

    static string IWorkflowStateful.WorkflowEntityType => "Invoice";

    /// <inheritdoc />
    public string GetWorkflowEntityId() => Id.ToString();

    // ── Draft-only mutations ───────────────────────────────────────────

    /// <summary>Adds a line item. Only allowed in Draft status.</summary>
    public void AddLineItem(InvoiceLineItem lineItem)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(lineItem);
        _lineItems.Add(lineItem);
        RecalculateSubtotal();
    }

    /// <summary>
    /// Sets the tax total from the <c>ITaxCalculator</c> result.
    /// Authoritative for rounding — may differ from sum of line item TaxAmount.
    /// </summary>
    public void SetTaxTotal(decimal taxTotal)
    {
        EnsureDraft();
        TaxTotal = taxTotal;
        Total = Subtotal + TaxTotal;
        RecalculateAmounts();
    }

    // ── Lifecycle transitions (idempotent) ─────────────────────────────

    /// <summary>
    /// Finalizes the document (Draft → Open). Assigns the document number and snapshots
    /// the party's billing address into <see cref="IssuedBillingAddressSnapshot"/> so the
    /// address shown on this legal document stays stable forever.
    /// </summary>
    /// <param name="documentNumber">Sequential document number assigned by the number generator.</param>
    /// <param name="issuedAt">Timestamp at which the document is issued.</param>
    /// <param name="dueAt">Payment due date (ignored for credit notes).</param>
    /// <param name="billingAddressSnapshot">
    /// Live <c>Party.BillingAddress</c> at the moment of finalization. Optional only because
    /// some test fixtures and fully tax-exempt cases legitimately have no address; production
    /// callers should always pass a snapshot fetched from <c>IPartyReader.GetByIdAsync</c>.
    /// </param>
    public bool Finalize(
        string documentNumber,
        DateTimeOffset issuedAt,
        DateTimeOffset? dueAt,
        Granit.Parties.Domain.BillingAddress? billingAddressSnapshot = null)
    {
        if (Status == InvoiceStatus.Open)
        {
            return false;
        }

        EnsureDraft();
        ArgumentException.ThrowIfNullOrWhiteSpace(documentNumber);

        Status = InvoiceStatus.Open;
        InvoiceNumber = documentNumber;
        IssuedAt = issuedAt;
        DueAt = IsCreditNote ? null : dueAt;
        IssuedBillingAddressSnapshot = billingAddressSnapshot;

        if (IsCreditNote)
        {
            AddDistributedEvent(new CreditNoteIssuedEto(
                Id, ParentInvoiceId!, TenantId!.Value, PartyId.Value, Total));
        }
        else
        {
            AddDistributedEvent(new InvoiceFinalizedEto(
                Id, TenantId!.Value, PartyId.Value, Total, Currency, CollectionMethod));
        }

        return true;
    }

    /// <summary>
    /// Records a payment (partial or full). Auto-transitions to Paid when
    /// AmountRemaining reaches zero (within tolerance).
    /// </summary>
    /// <param name="amount">Payment amount received.</param>
    /// <param name="paidAt">Timestamp of payment.</param>
    /// <param name="tolerance">
    /// Underpayment tolerance — if remaining amount is within this threshold,
    /// the invoice is considered fully paid. Default: 0.
    /// </param>
    /// <returns><c>true</c> if status changed to Paid; <c>false</c> if still Open.</returns>
    public bool RecordPayment(decimal amount, DateTimeOffset paidAt, decimal tolerance = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        ArgumentOutOfRangeException.ThrowIfNegative(tolerance);

        if (tolerance > Total * 0.05m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tolerance), tolerance, $"Tolerance cannot exceed 5% of invoice total ({Total}).");
        }

        if (Status == InvoiceStatus.Paid)
        {
            return false;
        }

        if (Status is not (InvoiceStatus.Open or InvoiceStatus.Uncollectible))
        {
            throw new InvalidOperationException(
                $"Cannot record payment on invoice '{Id}' in '{Status}' status.");
        }

        AmountPaid += amount;
        RecalculateAmounts();

        if (AmountRemaining <= tolerance)
        {
            PaidAt = paidAt;
            Status = InvoiceStatus.Paid;
            AddDistributedEvent(new InvoicePaidEto(Id, TenantId!.Value, PartyId.Value, paidAt));

            if (Overpayment > 0)
            {
                AddDistributedEvent(new OverpaymentDetectedEto(
                    Id, TenantId!.Value, PartyId.Value, Overpayment, Currency));
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Applies a credit note amount. Auto-transitions to Paid when
    /// AmountRemaining reaches zero (invoice paid entirely by credits, no payment needed).
    /// </summary>
    /// <param name="amount">Credit note amount to apply.</param>
    /// <param name="creditNoteIssuedAt">Timestamp of credit note issuance.</param>
    /// <returns><c>true</c> if status changed to Paid; <c>false</c> if still Open.</returns>
    public bool ApplyCreditNote(decimal amount, DateTimeOffset creditNoteIssuedAt)
    {
        if (Status == InvoiceStatus.Paid)
        {
            return false;
        }

        if (Status is not (InvoiceStatus.Open or InvoiceStatus.Uncollectible))
        {
            throw new InvalidOperationException(
                $"Cannot apply credit note on invoice '{Id}' in '{Status}' status.");
        }

        AmountCredited += amount;
        RecalculateAmounts();

        if (AmountRemaining <= 0)
        {
            PaidAt = creditNoteIssuedAt;
            Status = InvoiceStatus.Paid;
            AddDistributedEvent(new InvoicePaidEto(Id, TenantId!.Value, PartyId.Value, creditNoteIssuedAt));
            return true;
        }

        return false;
    }

    /// <summary>Voids the document (cancels, preserves paper trail).</summary>
    /// <remarks>
    /// Invoices with recorded payments cannot be voided — issue a credit note instead.
    /// </remarks>
    public bool VoidInvoice()
    {
        if (Status == InvoiceStatus.Void)
        {
            return false;
        }

        if (Status is not (InvoiceStatus.Open or InvoiceStatus.Uncollectible))
        {
            throw new InvalidOperationException(
                $"Cannot void document '{Id}' from '{Status}' status.");
        }

        if (AmountPaid > 0)
        {
            throw new InvalidOperationException(
                $"Cannot void document '{Id}' with recorded payments ({AmountPaid} {Currency}). Issue a credit note instead.");
        }

        Status = InvoiceStatus.Void;
        AddDistributedEvent(new InvoiceVoidedEto(Id, TenantId!.Value));
        return true;
    }

    /// <summary>Marks the invoice as uncollectible (bad debt, invoices only).</summary>
    public bool MarkUncollectible()
    {
        if (Status == InvoiceStatus.Uncollectible)
        {
            return false;
        }

        if (Status != InvoiceStatus.Open)
        {
            throw new InvalidOperationException(
                $"Cannot mark invoice '{Id}' as uncollectible from '{Status}' status.");
        }

        Status = InvoiceStatus.Uncollectible;
        AddDistributedEvent(new InvoiceUncollectibleEto(Id, TenantId!.Value));
        return true;
    }

    // ── External references & documents ────────────────────────────────

    /// <summary>Adds an external reference (Stripe, Odoo).</summary>
    public void AddExternalReference(InvoiceExternalReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        _externalReferences.Add(reference);
    }

    /// <summary>Adds a generated document (PDF).</summary>
    public void AddDocument(InvoiceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _documents.Add(document);
    }

    // ── Private helpers ────────────────────────────────────────────────

    private void RecalculateSubtotal()
    {
        Subtotal = _lineItems.Sum(li => li.Amount);
        Total = Subtotal + TaxTotal;
        RecalculateAmounts();
    }

    private void RecalculateAmounts()
    {
        decimal covered = AmountPaid + AmountCredited;
        AmountRemaining = Math.Max(0, Total - covered);
        Overpayment = Math.Max(0, covered - Total);
    }

    private void EnsureDraft()
    {
        if (Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException(
                $"Document '{Id}' is in '{Status}' status. Only Draft documents can be modified.");
        }
    }
}
