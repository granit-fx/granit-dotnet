namespace Granit.Scheduling.Endpoints.Dtos;

/// <summary>
/// Request DTO for rescheduling a scheduled action.
/// </summary>
/// <param name="NewExecuteAt">The new UTC execution date.</param>
public sealed record RescheduleActionRequest(DateTimeOffset NewExecuteAt);
