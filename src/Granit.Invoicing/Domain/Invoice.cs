using Granit.Domain;
using Granit.Invoicing.Domain.ValueObjects;
using Granit.Invoicing.Events;
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
    public static Invoice Create(
        Guid id,
        Guid tenantId,
        InvoiceDocumentType documentType,
        string currency,
        CollectionMethod collectionMethod,
        BillingReason billingReason,
        BillingAddress? billingAddress = null,
        InvoiceId? parentInvoiceId = null,
        string? creditNoteReason = null,
        DateTimeOffset? periodStart = null,
        DateTimeOffset? periodEnd = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        if (documentType == InvoiceDocumentType.CreditNote && parentInvoiceId is null)
        {
            throw new ArgumentException(
                "Credit notes must reference a parent invoice.", nameof(parentInvoiceId));
        }

        var invoice = new Invoice
        {
            Id = id,
            TenantId = tenantId,
            DocumentType = documentType,
            Currency = currency,
            CollectionMethod = collectionMethod,
            BillingReason = billingReason,
            BillingAddress = billingAddress,
            ParentInvoiceId = parentInvoiceId,
            CreditNoteReason = creditNoteReason,
            Status = InvoiceStatus.Draft,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
        };

        invoice.AddDomainEvent(new InvoiceCreatedEvent(id, tenantId));
        return invoice;
    }

    /// <summary>Convenience factory for creating a credit note linked to a parent invoice.</summary>
    public static Invoice CreateCreditNote(
        Guid id,
        Guid tenantId,
        InvoiceId parentInvoiceId,
        string currency,
        string reason) =>
        Create(
            id, tenantId, InvoiceDocumentType.CreditNote, currency,
            CollectionMethod.Auto, BillingReason.Manual,
            parentInvoiceId: parentInvoiceId,
            creditNoteReason: reason);

    // ── Properties ─────────────────────────────────────────────────────

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

    /// <summary>Customer billing address.</summary>
    public BillingAddress? BillingAddress { get; private set; }

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

    /// <summary>Payments received (invoices only).</summary>
    public decimal AmountPaid { get; private set; }

    /// <summary>Remaining amount due (total - amountPaid).</summary>
    public decimal AmountRemaining { get; private set; }

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
    public Guid? TenantId { get; set; }

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

    /// <summary>Sets the billing address. Only allowed in Draft status.</summary>
    public void SetBillingAddress(BillingAddress address)
    {
        EnsureDraft();
        BillingAddress = address;
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
        AmountRemaining = Total - AmountPaid;
    }

    // ── Lifecycle transitions (idempotent) ─────────────────────────────

    /// <summary>Finalizes the document (Draft → Open). Assigns document number.</summary>
    public bool Finalize(string documentNumber, DateTimeOffset issuedAt, DateTimeOffset? dueAt)
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

        if (IsCreditNote)
        {
            AddDistributedEvent(new CreditNoteIssuedEto(
                Id, ParentInvoiceId!, TenantId!.Value, Total));
        }
        else
        {
            AddDistributedEvent(new InvoiceFinalizedEto(
                Id, TenantId!.Value, Total, Currency, CollectionMethod));
        }

        return true;
    }

    /// <summary>Marks the invoice as paid (invoices only).</summary>
    public bool MarkPaid(decimal amountPaid, DateTimeOffset paidAt)
    {
        if (Status == InvoiceStatus.Paid)
        {
            return false;
        }

        if (Status is not (InvoiceStatus.Open or InvoiceStatus.Uncollectible))
        {
            throw new InvalidOperationException(
                $"Cannot mark invoice '{Id}' as paid from '{Status}' status.");
        }

        AmountPaid = amountPaid;
        AmountRemaining = Total - AmountPaid;
        PaidAt = paidAt;
        Status = InvoiceStatus.Paid;

        AddDistributedEvent(new InvoicePaidEto(Id, TenantId!.Value, paidAt));
        return true;
    }

    /// <summary>Voids the document (cancels, preserves paper trail).</summary>
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
        AmountRemaining = Total - AmountPaid;
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
