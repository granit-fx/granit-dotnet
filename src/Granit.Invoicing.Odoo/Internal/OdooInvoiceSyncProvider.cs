using Granit.Guids;
using Granit.Invoicing.Domain;
using Granit.Invoicing.Dtos;
using Granit.Invoicing.Odoo.Domain;
using Microsoft.Extensions.Logging;

namespace Granit.Invoicing.Odoo.Internal;

/// <summary>
/// Odoo implementation of <see cref="IInvoiceSyncProvider"/>.
/// Maps Granit invoices to Odoo <c>account.move</c> records via JSON-RPC.
/// </summary>
/// <remarks>
/// Partner management: on each sync, the tenant's <c>res.partner</c> is created
/// (first time) or updated (subsequent) with the latest billing address.
/// This ensures Odoo always reflects the current legal address.
/// </remarks>
internal sealed partial class OdooInvoiceSyncProvider(
    OdooJsonRpcClient rpcClient,
    IOdooPartnerMappingStore partnerMappingStore,
    IGuidGenerator guidGenerator,
    Microsoft.Extensions.Options.IOptions<Options.OdooOptions> options,
    ILogger<OdooInvoiceSyncProvider> logger) : IInvoiceSyncProvider
{
    /// <inheritdoc/>
    public string Name => "odoo";

    /// <inheritdoc/>
    public async Task<(string ProviderName, string ExternalId)> SyncAsync(
        Invoice invoice, CancellationToken cancellationToken = default)
    {
        // 1. Ensure Odoo partner exists and is up-to-date
        int partnerId = await GetOrCreatePartnerAsync(
            invoice.TenantId!.Value, invoice.BillingAddress, cancellationToken)
            .ConfigureAwait(false);

        // 2. Map Granit Invoice → Odoo account.move
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
            ["partner_id"] = partnerId,
            ["journal_id"] = options.Value.DefaultJournalId,
            ["ref"] = invoice.InvoiceNumber,
            ["invoice_date"] = invoice.IssuedAt?.ToString("yyyy-MM-dd"),
            ["invoice_date_due"] = invoice.DueAt?.ToString("yyyy-MM-dd"),
            ["invoice_line_ids"] = lineValues.Select(lv =>
                new object[] { 0, 0, lv }).ToList(),
        };

        int odooId = await rpcClient.CreateAsync("account.move", values, cancellationToken)
            .ConfigureAwait(false);

        Log.InvoiceSynced(logger, invoice.Id, odooId, partnerId);

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
        await Task.CompletedTask.ConfigureAwait(false);
        return null;
    }

    /// <summary>
    /// Gets or creates the Odoo partner for a tenant. On HIT, updates the partner
    /// with the latest billing address to keep Odoo in sync.
    /// </summary>
    private async Task<int> GetOrCreatePartnerAsync(
        Guid tenantId, BillingAddress? address,
        CancellationToken cancellationToken)
    {
        Dictionary<string, object?> partnerValues = MapAddressToPartner(address);

        OdooPartnerMapping? mapping = await partnerMappingStore
            .GetByTenantIdAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (mapping is not null)
        {
            // HIT: update partner with latest address
            await rpcClient.UpdateAsync("res.partner", mapping.OdooPartnerId, partnerValues, cancellationToken)
                .ConfigureAwait(false);

            Log.PartnerUpdated(logger, mapping.OdooPartnerId, tenantId);
            return mapping.OdooPartnerId;
        }

        // MISS: create partner in Odoo
        int partnerId = await rpcClient.CreateAsync("res.partner", partnerValues, cancellationToken)
            .ConfigureAwait(false);

        var newMapping = OdooPartnerMapping.Create(guidGenerator.Create(), tenantId, partnerId);
        await partnerMappingStore.AddAsync(newMapping, cancellationToken).ConfigureAwait(false);

        Log.PartnerCreated(logger, partnerId, tenantId);
        return partnerId;
    }

    private static Dictionary<string, object?> MapAddressToPartner(BillingAddress? address) => new()
    {
        ["name"] = address?.CompanyName ?? "Unknown",
        ["street"] = address?.Line1,
        ["city"] = address?.City,
        ["zip"] = address?.PostalCode,
        ["country_code"] = address?.Country,
        ["vat"] = address?.VatNumber,
        ["is_company"] = true,
    };

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Invoice {InvoiceId} synced to Odoo as account.move #{OdooId} (partner #{PartnerId})")]
        public static partial void InvoiceSynced(ILogger logger, Guid invoiceId, int odooId, int partnerId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Odoo partner #{PartnerId} created for tenant {TenantId}")]
        public static partial void PartnerCreated(ILogger logger, int partnerId, Guid tenantId);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Odoo partner #{PartnerId} updated for tenant {TenantId}")]
        public static partial void PartnerUpdated(ILogger logger, int partnerId, Guid tenantId);
    }
}
