using System.Globalization;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.Guids;
using Granit.Invoicing.Domain;
using Granit.Invoicing.Dtos;
using Granit.Parties;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Granit.Invoicing.Odoo.Internal;

/// <summary>
/// Odoo implementation of <see cref="IInvoiceSyncProvider"/>. Maps Granit invoices to Odoo
/// <c>account.move</c> records via JSON-RPC and stores the Odoo <c>res.partner</c> id
/// directly on the party through <see cref="Party.ExternalMappings"/> using the reserved
/// provider name <see cref="PartyExternalProviderNames.Odoo"/>.
/// </summary>
/// <remarks>
/// On each sync the party's <c>res.partner</c> is created (first time) or updated (subsequent)
/// with the latest billing address so Odoo always reflects the address shown on the issued
/// document.
/// </remarks>
internal sealed partial class OdooInvoiceSyncProvider(
    OdooJsonRpcClient rpcClient,
    IPartyReader partyReader,
    IPartyWriter partyWriter,
    IDataFilter dataFilter,
    IGuidGenerator guidGenerator,
    Microsoft.Extensions.Options.IOptions<Options.OdooOptions> options,
    ILogger<OdooInvoiceSyncProvider> logger) : IInvoiceSyncProvider
{
    /// <inheritdoc/>
    public string Name => PartyExternalProviderNames.Odoo;

    /// <inheritdoc/>
    public async Task<(string ProviderName, string ExternalId)> SyncAsync(
        Invoice invoice, CancellationToken cancellationToken = default)
    {
        // 1. Ensure Odoo partner exists and is up-to-date for the invoice's party.
        int partnerId = await GetOrCreatePartnerAsync(
            invoice.PartyId,
            invoice.IssuedBillingAddressSnapshot,
            cancellationToken).ConfigureAwait(false);

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
            ["invoice_date"] = invoice.IssuedAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["invoice_date_due"] = invoice.DueAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["invoice_line_ids"] = lineValues.Select(lv =>
                new object[] { 0, 0, lv }).ToList(),
        };

        int odooId = await rpcClient.CreateAsync("account.move", values, cancellationToken)
            .ConfigureAwait(false);

        Log.InvoiceSynced(logger, invoice.Id, odooId, partnerId);

        return (Name, odooId.ToString(CultureInfo.InvariantCulture));
    }

    /// <inheritdoc/>
    public async Task<ExternalInvoiceStatus?> GetStatusAsync(
        InvoiceExternalReference reference, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(reference.ExternalId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int odooId))
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
    /// Gets or creates the Odoo partner for a party. On HIT, updates the partner
    /// with the latest billing address to keep Odoo in sync.
    /// </summary>
    private async Task<int> GetOrCreatePartnerAsync(
        PartyId partyId,
        BillingAddress? address,
        CancellationToken cancellationToken)
    {
        Party party = await ResolvePartyAsync(partyId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Party '{partyId.Value}' not found — cannot sync the invoice to Odoo without a party to map to a res.partner.");

        Dictionary<string, object?> partnerValues = MapAddressToPartner(party, address);

        string? existing = party.FindExternalId(Name);
        if (existing is not null
            && int.TryParse(existing, NumberStyles.Integer, CultureInfo.InvariantCulture, out int existingPartnerId))
        {
            // HIT: refresh the partner with the latest address
            await rpcClient.UpdateAsync("res.partner", existingPartnerId, partnerValues, cancellationToken)
                .ConfigureAwait(false);
            Log.PartnerUpdated(logger, existingPartnerId, party.Id);
            return existingPartnerId;
        }

        // MISS: create the partner in Odoo and persist the reverse-link on the party.
        int partnerId = await rpcClient.CreateAsync("res.partner", partnerValues, cancellationToken)
            .ConfigureAwait(false);

        party.AddExternalMapping(guidGenerator.Create(), Name, partnerId.ToString(CultureInfo.InvariantCulture));
        await partyWriter.UpdateAsync(party, cancellationToken).ConfigureAwait(false);

        Log.PartnerCreated(logger, partnerId, party.Id);
        return partnerId;
    }

    private async Task<Party?> ResolvePartyAsync(PartyId partyId, CancellationToken cancellationToken)
    {
        // The party may be host-scoped (representing a tenant) or tenant-scoped — disable
        // the multi-tenant filter so the lookup succeeds regardless of the active scope.
        using IDisposable bypass = dataFilter.Disable<IMultiTenant>();
        return await partyReader.GetByIdAsync(partyId, cancellationToken).ConfigureAwait(false);
    }

    private static Dictionary<string, object?> MapAddressToPartner(
        Party party, BillingAddress? address) => new()
        {
            ["name"] = address?.CompanyName ?? party.Name,
            ["street"] = address?.Line1,
            ["city"] = address?.City,
            ["zip"] = address?.PostalCode,
            ["country_code"] = address?.Country,
            ["vat"] = address?.VatNumber ?? party.TaxId,
            ["is_company"] = party.Kind == PartyKind.Company,
        };

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Invoice {InvoiceId} synced to Odoo as account.move #{OdooId} (partner #{PartnerId})")]
        public static partial void InvoiceSynced(ILogger logger, Guid invoiceId, int odooId, int partnerId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Odoo partner #{PartnerId} created for party {PartyId}")]
        public static partial void PartnerCreated(ILogger logger, int partnerId, Guid partyId);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Odoo partner #{PartnerId} updated for party {PartyId}")]
        public static partial void PartnerUpdated(ILogger logger, int partnerId, Guid partyId);
    }
}
