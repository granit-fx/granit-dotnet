namespace Granit.Invoicing.Domain.ValueObjects;

/// <summary>
/// Identifies the origin of an invoice line item (e.g. subscription, metering, one-shot).
/// </summary>
/// <param name="Type">Source type (subscription, metering, one-shot, etc.).</param>
/// <param name="Id">Optional source entity identifier for traceability.</param>
public sealed record LineItemSource(InvoiceSourceType Type, string? Id = null);
