namespace Granit.Testing.Persistence.Domain;

/// <summary>Status enum used by the enum-persistence conformance suite (string column expected).</summary>
public enum ConformanceOrderStatus
{
    /// <summary>Initial state.</summary>
    Pending,

    /// <summary>Approved state.</summary>
    Approved,

    /// <summary>Rejected state.</summary>
    Rejected,
}
