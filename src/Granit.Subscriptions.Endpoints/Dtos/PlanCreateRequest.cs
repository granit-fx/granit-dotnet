namespace Granit.Subscriptions.Endpoints.Dtos;

/// <summary>Request to create a new plan in Draft status.</summary>
public sealed record PlanCreateRequest(
    string Name,
    string? Description,
    string PricingModel,
    string DefaultInterval,
    int? TrialDays = null,
    int? SeatLimit = null);

/// <summary>Request to update a draft plan.</summary>
public sealed record PlanUpdateRequest(
    string Name,
    string? Description,
    int SortOrder);
