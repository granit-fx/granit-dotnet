using Granit.Payments.SepaDirectDebit.Domain;

namespace Granit.Payments.SepaDirectDebit.Contracts;

/// <summary>Request to set up a new SEPA DD mandate.</summary>
public sealed record MandateSetupRequest(
    Guid TenantId, string DebtorName, string DebtorIban,
    string? DebtorBic, SddScheme Scheme, string? RedirectUrl);

/// <summary>Result of mandate setup.</summary>
public sealed record MandateSetupResult(
    string ProviderMandateId, string? RedirectUrl, MandateStatus Status);
