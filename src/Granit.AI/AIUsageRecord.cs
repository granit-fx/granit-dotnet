namespace Granit.AI;

/// <summary>
/// Records token consumption and cost for a single AI interaction.
/// </summary>
public sealed record AIUsageRecord
{
    /// <summary>Unique identifier for this record.</summary>
    public required Guid Id { get; init; }

    /// <summary>Tenant that made the request, or <c>null</c> if no tenant context.</summary>
    public Guid? TenantId { get; init; }

    /// <summary>
    /// Canonical <see cref="Granit.Identity.Domain.User.Id"/> that made
    /// the request (per ADR-051), or <c>null</c> if anonymous / system.
    /// The same Guid resolves both the local and federated login paths.
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>Workspace name used for the request.</summary>
    public required string WorkspaceName { get; init; }

    /// <summary>Provider name (e.g. <c>OpenAI</c>, <c>Anthropic</c>).</summary>
    public required string Provider { get; init; }

    /// <summary>Model identifier (e.g. <c>gpt-4o</c>, <c>claude-sonnet-4-6</c>).</summary>
    public required string Model { get; init; }

    /// <summary>Number of input tokens consumed.</summary>
    public int InputTokens { get; init; }

    /// <summary>Number of output tokens generated.</summary>
    public int OutputTokens { get; init; }

    /// <summary>Estimated cost (based on configured pricing).</summary>
    public decimal? EstimatedCost { get; init; }

    /// <summary>ISO 4217 currency code for <see cref="EstimatedCost"/> (e.g. <c>USD</c>, <c>CNY</c>).</summary>
    public string? CostCurrency { get; init; }

    /// <summary>When the interaction occurred (UTC).</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Duration of the AI call.</summary>
    public TimeSpan? Duration { get; init; }
}
