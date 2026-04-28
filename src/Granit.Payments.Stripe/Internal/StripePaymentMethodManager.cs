using Granit.DataFiltering;
using Granit.Domain;
using Granit.Guids;
using Granit.Parties;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;
using Granit.Payments.Contracts;
using Microsoft.Extensions.Logging;
using Stripe;
using PaymentMethod = Stripe.PaymentMethod;

namespace Granit.Payments.Stripe.Internal;

/// <summary>
/// Stripe payment method management (list, attach, detach). Reads and writes the
/// Stripe customer identifier through <see cref="Party.ExternalMappings"/> using
/// the reserved provider name <see cref="PartyExternalProviderNames.Stripe"/>.
/// </summary>
internal sealed partial class StripePaymentMethodManager(
    IStripeClient stripeClient,
    IPartyReader partyReader,
    IPartyWriter partyWriter,
    IDataFilter dataFilter,
    IGuidGenerator guidGenerator,
    ILogger<StripePaymentMethodManager> logger) : IPaymentMethodManager
{
    private const string Provider = PartyExternalProviderNames.Stripe;

    /// <inheritdoc/>
    public string ProviderName => Provider;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PaymentProviderMethod>> ListAsync(
        Guid partyId, CancellationToken cancellationToken = default)
    {
        Party? party = await ResolvePartyAsync(partyId, cancellationToken).ConfigureAwait(false);
        string? stripeCustomerId = party?.FindExternalId(Provider);

        if (stripeCustomerId is null)
        {
            return [];
        }

        var service = new PaymentMethodService(stripeClient);
        StripeList<PaymentMethod> methods = await service.ListAsync(
            new PaymentMethodListOptions { Customer = stripeCustomerId },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return methods.Data.Select(m => new PaymentProviderMethod(
            ProviderMethodId: m.Id,
            MethodType: StripePaymentMethodTypeMapper.FromStripeType(m.Type),
            DisplayLabel: BuildDisplayLabel(m),
            ExpiresAt: m.Card?.ExpYear is not null
                ? new DateTimeOffset((int)m.Card.ExpYear, (int)m.Card.ExpMonth, 1, 0, 0, 0, TimeSpan.Zero)
                : null
        )).ToList();
    }

    /// <inheritdoc/>
    public async Task<PaymentProviderMethod> AttachAsync(
        PaymentAttachMethodRequest request, CancellationToken cancellationToken = default)
    {
        string customerId = await GetOrCreateStripeCustomerAsync(request.PartyId, cancellationToken)
            .ConfigureAwait(false);

        var service = new PaymentMethodService(stripeClient);
        PaymentMethod method = await service.AttachAsync(
            request.Token,
            new PaymentMethodAttachOptions { Customer = customerId },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        Log.PaymentMethodAttached(logger, method.Id, request.PartyId);

        return new PaymentProviderMethod(
            ProviderMethodId: method.Id,
            MethodType: StripePaymentMethodTypeMapper.FromStripeType(method.Type),
            DisplayLabel: BuildDisplayLabel(method),
            ExpiresAt: method.Card?.ExpYear is not null
                ? new DateTimeOffset((int)method.Card.ExpYear, (int)method.Card.ExpMonth, 1, 0, 0, 0, TimeSpan.Zero)
                : null);
    }

    /// <inheritdoc/>
    public async Task DetachAsync(
        string providerMethodId, CancellationToken cancellationToken = default)
    {
        var service = new PaymentMethodService(stripeClient);
        await service.DetachAsync(providerMethodId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        Log.PaymentMethodDetached(logger, providerMethodId);
    }

    private async Task<string> GetOrCreateStripeCustomerAsync(
        Guid partyId, CancellationToken cancellationToken)
    {
        Party party = await ResolvePartyAsync(partyId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Party '{partyId}' not found — cannot attach a Stripe payment method to a non-existent party.");

        string? existing = party.FindExternalId(Provider);
        if (existing is not null)
        {
            return existing;
        }

        var service = new CustomerService(stripeClient);
        Customer customer = await service.CreateAsync(
            new CustomerCreateOptions
            {
                Name = party.Name,
                Metadata = new Dictionary<string, string> { ["granit_party_id"] = party.Id.ToString() },
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        party.AddExternalMapping(guidGenerator.Create(), Provider, customer.Id);
        await partyWriter.UpdateAsync(party, cancellationToken).ConfigureAwait(false);

        Log.CustomerCreated(logger, customer.Id, partyId);

        return customer.Id;
    }

    private async Task<Party?> ResolvePartyAsync(Guid partyId, CancellationToken cancellationToken)
    {
        // The party may be host-scoped or tenant-scoped — bypass the tenant filter so a
        // host-side payment workflow can resolve a tenant-scoped party, and vice versa.
        using IDisposable bypass = dataFilter.Disable<IMultiTenant>();
        return await partyReader
            .GetByIdAsync(PartyId.Create(partyId), cancellationToken)
            .ConfigureAwait(false);
    }

    private static string BuildDisplayLabel(PaymentMethod method) => method.Type switch
    {
        "card" => $"**** {method.Card?.Last4}",
        "sepa_debit" => $"IBAN ***{method.SepaDebit?.Last4}",
        _ => method.Type,
    };

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Stripe payment method attached: {MethodId} for party {PartyId}")]
        public static partial void PaymentMethodAttached(ILogger logger, string methodId, Guid partyId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Stripe payment method detached: {MethodId}")]
        public static partial void PaymentMethodDetached(ILogger logger, string methodId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Stripe customer created: {CustomerId} for party {PartyId}")]
        public static partial void CustomerCreated(ILogger logger, string customerId, Guid partyId);
    }
}
