using Granit.Subscriptions.Domain;
using Granit.Workflow.Domain;

namespace Granit.Subscriptions.Endpoints.Dtos;

/// <summary>Plan catalog entry.</summary>
public sealed record PlanResponse(
    Guid Id,
    string Name,
    string? Description,
    string PricingModel,
    string DefaultInterval,
    int? TrialDays,
    int? SeatLimit,
    int SortOrder,
    string LifecycleStatus,
    IReadOnlyList<PlanPriceResponse> Prices)
{
    internal static PlanResponse FromEntity(Plan plan) => new(
        plan.Id,
        plan.Name,
        plan.Description,
        plan.PricingModel.ToString(),
        plan.DefaultInterval.ToString(),
        plan.TrialDays,
        plan.SeatLimit,
        plan.SortOrder,
        plan.LifecycleStatus.ToString(),
        plan.Prices.Select(PlanPriceResponse.FromEntity).ToList());
}

/// <summary>Plan price entry with versioning metadata.</summary>
public sealed record PlanPriceResponse(
    Guid Id,
    decimal Amount,
    string Currency,
    string Interval,
    DateTimeOffset EffectiveFrom,
    bool IsCurrent,
    Guid? ReplacedByPriceId = null,
    DateTimeOffset? ReplacedAt = null)
{
    internal static PlanPriceResponse FromEntity(PlanPrice price) =>
        new(price.Id, price.Amount, price.Currency, price.Interval.ToString(),
            price.EffectiveFrom, price.IsCurrent, price.ReplacedByPriceId, price.ReplacedAt);
}
