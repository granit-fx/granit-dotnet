namespace Granit.Privacy.AI;

/// <summary>
/// Result of a PII detection scan on a text fragment.
/// </summary>
public sealed record PiiDetectionResult
{
    /// <summary>
    /// Whether the scanned text contains any personally identifiable information.
    /// </summary>
    public required bool ContainsPii { get; init; }

    /// <summary>
    /// Detected PII items with their type and description.
    /// Empty when <see cref="ContainsPii"/> is <c>false</c>.
    /// </summary>
    public required IReadOnlyList<DetectedPii> Items { get; init; }
}

/// <summary>
/// A single PII item detected in text.
/// </summary>
/// <param name="Type">The category of PII detected.</param>
/// <param name="Description">Human-readable description of where/what was detected (never contains the actual PII value).</param>
public sealed record DetectedPii(PiiType Type, string Description);

/// <summary>
/// Categories of personally identifiable information.
/// </summary>
public enum PiiType
{
    /// <summary>A person's name (first, last, or full).</summary>
    PersonName,

    /// <summary>An email address.</summary>
    Email,

    /// <summary>A phone number (any format).</summary>
    PhoneNumber,

    /// <summary>A physical or mailing address.</summary>
    Address,

    /// <summary>A national identification number (SSN, NISS, BSN, etc.).</summary>
    NationalId,

    /// <summary>A date of birth.</summary>
    DateOfBirth,

    /// <summary>A bank account number (IBAN, routing number, etc.).</summary>
    BankAccount,

    /// <summary>A credit or debit card number.</summary>
    CreditCard,

    /// <summary>Any other PII category not listed above.</summary>
    Other,
}
