using Granit.DataProtection;

namespace Granit.Http.ExceptionHandling.Internal;

/// <summary>
/// Redacts sensitive field values from validation error payloads before they
/// are serialized into the <c>ProblemDetails.Extensions["errors"]</c> object.
/// </summary>
/// <remarks>
/// <para>
/// FluentValidation and ModelState commonly echo the offending value in the
/// error message (<c>"'Password' must not equal 'hunter2'"</c>,
/// <c>"Email 'alice@x.com' is not a valid address"</c>). If the field is marked
/// <see cref="SensitiveDataAttribute"/>, the value ends up in the client
/// response — a GDPR Art. 32 confidentiality violation and a vector for PII
/// or credential disclosure.
/// </para>
/// <para>
/// This sanitizer preserves the error key so the front-end keeps its
/// field-error binding, but replaces each message with a neutral
/// <c>"Invalid value."</c> when any segment of the property path matches a
/// registered sensitive property. Property paths support both MVC ModelState
/// (<c>"Address.SecretCode"</c>) and FluentValidation collection syntax
/// (<c>"Users[0].Password"</c>, <c>"Items[3].Card.Cvv"</c>); purely numeric
/// segments (collection indices) are skipped.
/// </para>
/// </remarks>
internal sealed class ValidationErrorsSanitizer(SensitivePropertyRegistry? registry)
{
    private const string RedactedMessage = "Invalid value.";

    private static readonly char[] PathSeparators = ['.', '[', ']'];

    public IReadOnlyDictionary<string, string[]> Sanitize(
        IReadOnlyDictionary<string, string[]> errors)
    {
        if (registry is null || errors.Count == 0)
        {
            return errors;
        }

        // Two-pass: detect whether any key needs redaction before allocating.
        // Preserves the original dictionary instance when nothing is sensitive
        // (common case for non-PII endpoints), and avoids the ordering bug
        // where a sensitive key found after non-sensitive keys would leave the
        // earlier ones out of the rebuilt dictionary.
        if (!errors.Keys.Any(IsPathSensitive))
        {
            return errors;
        }

        Dictionary<string, string[]> sanitized = new(errors.Count, StringComparer.Ordinal);
        foreach (KeyValuePair<string, string[]> pair in errors)
        {
            sanitized[pair.Key] = IsPathSensitive(pair.Key)
                ? [RedactedMessage]
                : pair.Value;
        }

        return sanitized;
    }

    private bool IsPathSensitive(string errorKey)
    {
        foreach (string segment in errorKey.Split(PathSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            // Skip purely-numeric segments (collection indices like "0" in "Users[0].Password").
            if (IsPurelyNumeric(segment))
            {
                continue;
            }

            if (registry!.IsSensitiveAtLevel(segment, Sensitivity.Confidential, out _))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPurelyNumeric(string segment) =>
        segment.Length > 0 && segment.All(c => c is >= '0' and <= '9');
}
