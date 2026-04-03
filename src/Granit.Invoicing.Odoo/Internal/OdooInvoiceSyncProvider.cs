using Granit.Invoicing.Domain;
using Granit.Invoicing.Dtos;
using Microsoft.Extensions.Logging;

namespace Granit.Invoicing.Odoo.Internal;

/// <summary>
/// Odoo implementation of <see cref="IInvoiceSyncProvider"/>.
/// Maps Granit invoices to Odoo <c>account.move</c> records via JSON-RPC.
/// </summary>
internal sealed partial class OdooInvoiceSyncProvider(
    OdooJsonRpcClient rpcClient,
    Microsoft.Extensions.Options.IOptions<Options.OdooOptions> options,
    ILogger<OdooInvoiceSyncProvider> logger) : IInvoiceSyncProvider
{
    /// <inheritdoc/>
    public string Name => "odoo";

    /// <inheritdoc/>
    public async Task<(string ProviderName, string ExternalId)> SyncAsync(
        Invoice invoice, CancellationToken cancellationToken = default)
    {
        // Map Granit Invoice → Odoo account.move
        var lineValues = invoice.LineItems.Select(li => new Dictionary<string, object?>
        {
            ["name"] = li.Description,
            ["quantity"] = (object)li.Quantity,
            ["price_unit"] = (object)li.UnitPrice,
        }).ToList();

        string moveType = invoice.IsCreditNote ? "out_refund" : "out_invoice";

        var values = new Dictionary<string, object?>
        {
            ["move_type"] = moveType,
            ["journal_id"] = options.Value.DefaultJournalId,
            ["ref"] = invoice.InvoiceNumber,
            ["invoice_date"] = invoice.IssuedAt?.ToString("yyyy-MM-dd"),
            ["invoice_date_due"] = invoice.DueAt?.ToString("yyyy-MM-dd"),
            ["currency_id"] = null, // Odoo resolves from company default
            ["invoice_line_ids"] = lineValues.Select(lv =>
                new object[] { 0, 0, lv }).ToList(),
        };

        int odooId = await rpcClient.CreateAsync("account.move", values, cancellationToken)
            .ConfigureAwait(false);

        Log.InvoiceSynced(logger, invoice.Id, odooId);

        return (Name, odooId.ToString());
    }

    /// <inheritdoc/>
    public async Task<ExternalInvoiceStatus?> GetStatusAsync(
        InvoiceExternalReference reference, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(reference.ExternalId, out int odooId))
        {
            return null;
        }

        System.Text.Json.JsonElement? result = await rpcClient
            .ReadAsync("account.move", odooId, ["state", "payment_state"], cancellationToken)
            .ConfigureAwait(false);

        if (result is null)
        {
            return null;
        }

        string? paymentState = result.Value.ValueKind == System.Text.Json.JsonValueKind.Array
            ? result.Value[0].GetProperty("payment_state").GetString()
            : null;

        return paymentState switch
        {
            "paid" or "in_payment" => ExternalInvoiceStatus.Paid,
            "reversed" => ExternalInvoiceStatus.Voided,
            _ => ExternalInvoiceStatus.Synced,
        };
    }

    /// <inheritdoc/>
    public async Task<Stream?> GetDocumentAsync(
        InvoiceExternalReference reference, CancellationToken cancellationToken = default)
    {
        // Odoo PDF retrieval requires a report action call — Phase 3
        // For now, return null (no PDF available from Odoo)
        await Task.CompletedTask.ConfigureAwait(false);
        return null;
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Invoice {InvoiceId} synced to Odoo as account.move #{OdooId}")]
        public static partial void InvoiceSynced(ILogger logger, Guid invoiceId, int odooId);
    }
}
