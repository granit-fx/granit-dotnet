using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Users;

namespace Granit.AI.Internal;

/// <summary>
/// Creates <see cref="AIUsageRecord"/> instances with tenant and user context resolved from the current scope.
/// </summary>
internal sealed class AIUsageRecordFactory(
    ICurrentTenant currentTenant,
    ICurrentUserService currentUserService,
    IGuidGenerator guidGenerator,
    TimeProvider timeProvider) : IAIUsageRecordFactory
{
    /// <inheritdoc/>
    public AIUsageRecord Create(
        string workspaceName,
        string provider,
        string model,
        int inputTokens,
        int outputTokens,
        TimeSpan? duration) => new()
        {
            Id = guidGenerator.Create(),
            TenantId = currentTenant.IsAvailable ? currentTenant.Id : null,
            UserId = Guid.TryParse(currentUserService.UserId, out Guid uid) ? uid : null,
            WorkspaceName = workspaceName,
            Provider = provider,
            Model = model,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            Timestamp = timeProvider.GetUtcNow(),
            Duration = duration,
        };
}
