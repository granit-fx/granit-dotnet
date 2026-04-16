namespace Granit.Subscriptions.Endpoints.Dtos;

/// <summary>Request to create a new price version for a plan.</summary>
public sealed record CreatePriceVersionRequest(
    decimal Amount,
    string Currency,
    string Interval);

/// <summary>Request to migrate a subscription to a new price version.</summary>
public sealed record MigratePriceRequest(Guid NewPlanPriceId);

/// <summary>Request to bulk-migrate subscriptions to a new price version.</summary>
public sealed record BulkMigratePriceRequest(
    Guid PlanId,
    Guid NewPlanPriceId,
    Guid? OldPlanPriceId = null);

/// <summary>Response for bulk price migration.</summary>
public sealed record BulkMigratePriceResponse(int MigratedCount);
