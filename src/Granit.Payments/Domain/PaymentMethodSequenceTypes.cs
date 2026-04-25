namespace Granit.Payments.Domain;

/// <summary>
/// Payment sequence modes, aligned with Mollie's <c>sequenceType</c> semantics.
/// </summary>
/// <remarks>
/// Callers request a single mode per operation. The availability filter matches a method
/// if its <see cref="PaymentMethodCapability.SupportedSequenceTypes"/> includes the requested
/// mode via bit test. Callers are responsible for chaining modes across a subscription
/// lifecycle (<see cref="First"/> to set up a mandate, then <see cref="Recurring"/> for
/// subsequent charges) — the filter does not infer this sequencing.
/// </remarks>
[Flags]
public enum PaymentMethodSequenceTypes
{
    /// <summary>No sequence declared. Invalid on a capability; used only as a sentinel.</summary>
    None = 0,

    /// <summary>Single payment with no stored mandate.</summary>
    OneOff = 1,

    /// <summary>Initial payment that establishes a mandate for later recurring charges.</summary>
    First = 2,

    /// <summary>Subsequent off-session payment using a previously established mandate.</summary>
    Recurring = 4,
}
