namespace Granit.Indexing.Exceptions;

/// <summary>
/// Thrown when a single principal exceeds
/// <see cref="Options.GranitIndexingOptions.MaxEmptyResultQueriesPerPrincipalPerMinute"/>
/// empty-result searches within a one-minute sliding window. Slows down existence-oracle
/// probing.
/// </summary>
/// <remarks>
/// Consumers convert this into <c>TypedResults.Problem(detail, statusCode: 429)</c> at the
/// HTTP boundary. The base framework does not synthesise the response so callers retain
/// control over the <c>application/problem+json</c> payload.
/// </remarks>
public sealed class EmptyResultRateLimitedException : Exception
{
    /// <summary>Stable identifier for the cause. Always <c>empty_result_rate_limited</c>.</summary>
    public string Reason { get; } = "empty_result_rate_limited";

    /// <summary>The principal identifier hashed before logging, never the raw value.</summary>
    public string PrincipalIdentifierHash { get; }

    public EmptyResultRateLimitedException(string principalIdentifierHash)
        : base("Empty-result search rate limit exceeded for principal.")
    {
        ArgumentException.ThrowIfNullOrEmpty(principalIdentifierHash);
        PrincipalIdentifierHash = principalIdentifierHash;
    }
}
