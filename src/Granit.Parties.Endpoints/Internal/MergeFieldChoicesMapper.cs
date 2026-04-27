using Granit.Mergeable;

namespace Granit.Parties.Endpoints.Internal;

/// <summary>
/// Maps the wire dictionary <c>{ "FieldPath": "Survivor" | "Loser" }</c> from the merge
/// request bodies to the strongly-typed <see cref="MergeFieldChoices"/> the orchestrator
/// expects. Validation of the values happens at the validator boundary; this mapper
/// trusts the input.
/// </summary>
internal static class MergeFieldChoicesMapper
{
    public static MergeFieldChoices FromDictionary(IReadOnlyDictionary<string, string>? wire)
    {
        if (wire is null || wire.Count == 0)
        {
            return MergeFieldChoices.Empty;
        }

        Dictionary<string, WinnerSide> mapped = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> kv in wire)
        {
            // Validator already enforces "Survivor" / "Loser" — Enum.Parse here covers
            // the happy path. An invalid value would have been rejected before reaching
            // the handler, so a defensive fallback isn't necessary.
            mapped[kv.Key] = Enum.Parse<WinnerSide>(kv.Value);
        }
        return new MergeFieldChoices(mapped);
    }
}
