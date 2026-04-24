namespace Granit.Subscriptions.Endpoints.Dtos;

/// <summary>Request to create a new price version for a plan.</summary>
/// <param name="Amount">Price amount in the smallest currency unit.</param>
/// <param name="Currency">ISO 4217 currency code (e.g., "EUR", "USD").</param>
/// <param name="Interval">Billing interval (Monthly, Yearly, ...).</param>
/// <param name="ProductId">
/// Optional reference to a <c>Granit.Catalog.Product</c> identifier — the catalog
/// item this price tarifs. Soft reference (no SQL FK across modules); preserved
/// across price versions.
/// </param>
public sealed record CreatePriceVersionRequest(
    decimal Amount,
    string Currency,
    string Interval,
    Guid? ProductId = null);

/// <summary>Request to migrate a subscription to a new price version.</summary>
public sealed record MigratePriceRequest(Guid NewPlanPriceId);

/// <summary>Request to bulk-migrate subscriptions to a new price version.</summary>
public sealed record BulkMigratePriceRequest(
    Guid PlanId,
    Guid NewPlanPriceId,
    Guid? OldPlanPriceId = null);

/// <summary>Response for bulk price migration.</summary>
public sealed record BulkMigratePriceResponse(int MigratedCount);
