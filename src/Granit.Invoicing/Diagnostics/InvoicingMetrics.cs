using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Invoicing.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the invoicing module.
/// Meter: <c>Granit.Invoicing</c>.
/// </summary>
public sealed class InvoicingMetrics
{
    /// <summary>The meter name used for all invoicing metrics.</summary>
    public const string MeterName = "Granit.Invoicing";

    private const string TenantIdTag = "tenant_id";
    private const string GlobalTenant = "global";

    private readonly Counter<long> _invoicesCreated;
    private readonly Counter<long> _invoicesFinalized;
    private readonly Counter<long> _invoicesPaid;
    private readonly Counter<long> _invoicesVoided;
    private readonly Counter<long> _creditNotesIssued;

    /// <summary>Initializes invoicing metrics using the specified meter factory.</summary>
    public InvoicingMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _invoicesCreated = meter.CreateCounter<long>(
            "granit.invoicing.invoice.created",
            description: "Number of invoices created.");

        _invoicesFinalized = meter.CreateCounter<long>(
            "granit.invoicing.invoice.finalized",
            description: "Number of invoices finalized.");

        _invoicesPaid = meter.CreateCounter<long>(
            "granit.invoicing.invoice.paid",
            description: "Number of invoices fully paid.");

        _invoicesVoided = meter.CreateCounter<long>(
            "granit.invoicing.invoice.voided",
            description: "Number of invoices voided.");

        _creditNotesIssued = meter.CreateCounter<long>(
            "granit.invoicing.credit_note.issued",
            description: "Number of credit notes issued.");
    }

    /// <summary>Records an invoice creation.</summary>
    public void RecordCreated(string? tenantId)
    {
        var tags = new TagList { { TenantIdTag, tenantId ?? GlobalTenant } };
        _invoicesCreated.Add(1, tags);
    }

    /// <summary>Records an invoice finalization.</summary>
    public void RecordFinalized(string? tenantId)
    {
        var tags = new TagList { { TenantIdTag, tenantId ?? GlobalTenant } };
        _invoicesFinalized.Add(1, tags);
    }

    /// <summary>Records an invoice paid.</summary>
    public void RecordPaid(string? tenantId)
    {
        var tags = new TagList { { TenantIdTag, tenantId ?? GlobalTenant } };
        _invoicesPaid.Add(1, tags);
    }

    /// <summary>Records an invoice voided.</summary>
    public void RecordVoided(string? tenantId)
    {
        var tags = new TagList { { TenantIdTag, tenantId ?? GlobalTenant } };
        _invoicesVoided.Add(1, tags);
    }

    /// <summary>Records a credit note issued.</summary>
    public void RecordCreditNoteIssued(string? tenantId)
    {
        var tags = new TagList { { TenantIdTag, tenantId ?? GlobalTenant } };
        _creditNotesIssued.Add(1, tags);
    }
}
