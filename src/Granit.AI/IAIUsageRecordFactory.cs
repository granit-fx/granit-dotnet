namespace Granit.AI;

/// <summary>
/// Factory for creating <see cref="AIUsageRecord"/> instances with tenant and user context
/// resolved from the current scope.
/// </summary>
public interface IAIUsageRecordFactory
{
    /// <summary>
    /// Creates an <see cref="AIUsageRecord"/> populated with tenant, user, and timestamp context.
    /// </summary>
    AIUsageRecord Create(
        string workspaceName,
        string provider,
        string model,
        int inputTokens,
        int outputTokens,
        TimeSpan? duration);
}
