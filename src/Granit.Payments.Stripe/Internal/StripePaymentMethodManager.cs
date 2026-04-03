using Granit.Guids;
using Granit.Payments.Contracts;
using Granit.Payments.Domain;

using Microsoft.Extensions.Logging;
using Stripe;
using PaymentMethod = Stripe.PaymentMethod;

namespace Granit.Payments.Stripe.Internal;

/// <summary>Stripe payment method management (list, attach, detach).</summary>
internal sealed partial class StripePaymentMethodManager(
    IStripeClient stripeClient,
    IProviderCustomerMappingStore customerMappingStore,
    IGuidGenerator guidGenerator,
    ILogger<StripePaymentMethodManager> logger) : IPaymentMethodManager
{
    /// <inheritdoc/>
    public string ProviderName => "stripe";

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PaymentProviderMethod>> ListAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        ProviderCustomerMapping? mapping = await customerMappingStore
            .GetAsync("stripe", tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (mapping is null)
        {
            return [];
        }

        var service = new PaymentMethodService(stripeClient);
        StripeList<PaymentMethod> methods = await service.ListAsync(
            new PaymentMethodListOptions { Customer = mapping.ProviderCustomerId },
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
        string customerId = await GetOrCreateCustomerAsync(request.TenantId, cancellationToken)
            .ConfigureAwait(false);

        var service = new PaymentMethodService(stripeClient);
        PaymentMethod method = await service.AttachAsync(
            request.Token,
            new PaymentMethodAttachOptions { Customer = customerId },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        Log.PaymentMethodAttached(logger, method.Id, request.TenantId);

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

    private async Task<string> GetOrCreateCustomerAsync(
        Guid tenantId, CancellationToken cancellationToken)
    {
        ProviderCustomerMapping? mapping = await customerMappingStore
            .GetAsync("stripe", tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (mapping is not null)
        {
            return mapping.ProviderCustomerId;
        }

        var service = new CustomerService(stripeClient);
        Customer customer = await service.CreateAsync(
            new CustomerCreateOptions
            {
                Metadata = new Dictionary<string, string> { ["granit_tenant_id"] = tenantId.ToString() },
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var newMapping = ProviderCustomerMapping.Create(guidGenerator.Create(), "stripe", tenantId, customer.Id);
        await customerMappingStore.AddAsync(newMapping, cancellationToken).ConfigureAwait(false);

        Log.CustomerCreated(logger, customer.Id, tenantId);

        return customer.Id;
    }

    private static string BuildDisplayLabel(PaymentMethod method) => method.Type switch
    {
        "card" => $"**** {method.Card?.Last4}",
        "sepa_debit" => $"IBAN ***{method.SepaDebit?.Last4}",
        _ => method.Type,
    };

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Stripe payment method attached: {MethodId} for tenant {TenantId}")]
        public static partial void PaymentMethodAttached(ILogger logger, string methodId, Guid tenantId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Stripe payment method detached: {MethodId}")]
        public static partial void PaymentMethodDetached(ILogger logger, string methodId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Stripe customer created: {CustomerId} for tenant {TenantId}")]
        public static partial void CustomerCreated(ILogger logger, string customerId, Guid tenantId);
    }
}
