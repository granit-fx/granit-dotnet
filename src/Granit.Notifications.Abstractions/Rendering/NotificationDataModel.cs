using System.Text.Json;

namespace Granit.Notifications.Rendering;

/// <summary>
/// Converts a notification <c>Data</c> payload (<see cref="JsonElement"/>) into the flat
/// dictionary shape template engines consume. Single shared implementation — this used to
/// live privately in the email channel.
/// </summary>
public static class NotificationDataModel
{
    /// <summary>
    /// One-level flatten of a JSON object. Nested objects/arrays surface as their raw JSON
    /// string — templates needing to iterate a list must ship a pre-joined <c>*Display</c>
    /// companion field (see notifications conventions).
    /// </summary>
    public static Dictionary<string, object?> ToDictionary(JsonElement element)
    {
        Dictionary<string, object?> dict = [];
        if (element.ValueKind != JsonValueKind.Object)
        {
            return dict;
        }

        foreach (JsonProperty prop in element.EnumerateObject())
        {
            dict[prop.Name] = prop.Value.ValueKind switch
            {
                // ISO-8601 strings surface as DateTime so Scriban date filters work —
                // DateTimeOffset payload properties round-trip through JSON as strings, and a
                // raw string makes `| date.to_string` fail. Scriban's date functions accept
                // DateTime, not DateTimeOffset; .DateTime keeps the sender's wall-clock time.
                JsonValueKind.String when prop.Value.TryGetDateTimeOffset(out DateTimeOffset dto) => dto.DateTime,
                JsonValueKind.String => prop.Value.GetString(),
                // Integers must stay integral: Scriban's range operator (`for i in 0..n`)
                // is not implemented for doubles.
                JsonValueKind.Number when prop.Value.TryGetInt64(out long integer) => integer,
                JsonValueKind.Number => prop.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => prop.Value.ToString(),
            };
        }

        return dict;
    }
}
