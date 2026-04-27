namespace Granit.Parties.Endpoints.Dtos;

/// <summary>Bulk-replace request for a party's free-form metadata dictionary.</summary>
/// <param name="Metadata">
/// New metadata dictionary. Pass an empty object to clear all entries.
/// Keys and values are case-sensitive strings; do NOT store PII here.
/// </param>
public sealed record PartyMetadataRequest(IReadOnlyDictionary<string, string> Metadata);
