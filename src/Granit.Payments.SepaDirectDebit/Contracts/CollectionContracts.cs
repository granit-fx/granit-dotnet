using Granit.Payments.SepaDirectDebit.Domain;

namespace Granit.Payments.SepaDirectDebit.Contracts;

/// <summary>Request to collect funds via direct debit.</summary>
public sealed record CollectionRequest(
    string ProviderMandateId, Guid InvoiceId,
    decimal Amount, string Currency, DateTimeOffset RequestedDate);

/// <summary>Result of a collection submission.</summary>
public sealed record CollectionResult(string ProviderCollectionId, CollectionStatus Status);

/// <summary>Status check result for a collection.</summary>
public sealed record CollectionStatusResult(
    CollectionStatus Status, string? FailureCode, string? FailureReason);

/// <summary>Generated PAIN.008 batch file.</summary>
public sealed record CollectionFileResult(
    byte[] Content, string FileName, int CollectionCount, decimal TotalAmount);
