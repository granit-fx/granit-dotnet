using Granit.Payments.Contracts;
using Granit.Payments.SepaTransfer.Options;
using Granit.Timing;
using Microsoft.Extensions.Options;

namespace Granit.Payments.SepaTransfer.Internal;

/// <summary>
/// Returns bank transfer instructions instead of a hosted payment page.
/// </summary>
/// <remarks>
/// The "checkout URL" is a JSON object with IBAN, BIC, beneficiary name,
/// structured reference, and amount. The frontend renders this as transfer
/// instructions to the customer.
/// </remarks>
internal sealed class SepaTransferCheckoutSessionFactory(
    StructuredReferenceGenerator referenceGenerator,
    IOptions<SepaTransferOptions> options,
    IClock clock) : ICheckoutSessionFactory
{
    /// <inheritdoc/>
    public string ProviderName => "sepa-transfer";

    /// <inheritdoc/>
    public Task<PaymentCheckoutSession> CreateAsync(
        PaymentCheckoutSessionRequest request, CancellationToken cancellationToken = default)
    {
        SepaTransferOptions config = options.Value;
        string reference = referenceGenerator.Generate(request.TransactionId);

        // Encode transfer instructions as a data URL (frontend parses this)
        string instructionsJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            iban = config.Iban,
            bic = config.Bic,
            beneficiary = config.BeneficiaryName,
            reference,
            amount = request.Amount,
            currency = request.Currency,
        });

        string dataUrl = $"data:application/json;base64,{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(instructionsJson))}";

        return Task.FromResult(new PaymentCheckoutSession(
            Url: dataUrl,
            SessionId: reference,
            ExpiresAt: clock.Now.AddDays(config.ExpirationDays)));
    }
}
