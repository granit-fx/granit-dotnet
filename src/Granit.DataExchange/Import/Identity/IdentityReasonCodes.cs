namespace Granit.DataExchange.Import.Identity;

/// <summary>
/// Structured reason codes attached to <see cref="RecordIdentity"/> for
/// <see cref="RecordOperation.Skip"/> and <see cref="RecordOperation.Ambiguous"/> outcomes,
/// and to the corresponding <c>ImportRowError</c> when the executor turns them into report entries.
/// </summary>
public static class IdentityReasonCodes
{
    /// <summary>A business/composite key already appeared earlier in the same file — the later row is skipped.</summary>
    public const string DuplicateKeyInFile = "Granit:DataExchange:Identity:DuplicateKeyInFile";

    /// <summary>One or more declared key components were null or empty on the incoming row.</summary>
    public const string MissingKeyComponent = "Granit:DataExchange:Identity:MissingKeyComponent";

    /// <summary>A strict <see cref="RecordOperation.Update"/> found no matching existing row.</summary>
    public const string MissingTarget = "Granit:DataExchange:Identity:MissingTarget";

    /// <summary>The identity resolver could not determine a single unambiguous match.</summary>
    public const string AmbiguousMatch = "Granit:DataExchange:Identity:AmbiguousMatch";
}
