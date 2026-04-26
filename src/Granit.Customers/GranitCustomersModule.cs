using Granit.Modularity;

namespace Granit.Customers;

/// <summary>
/// Granit module for the central <c>Customer</c> aggregate.
/// </summary>
/// <remarks>
/// Provides the domain model, reader/writer abstractions, and integration events.
/// Add <c>Granit.Customers.EntityFrameworkCore</c> for the EF Core persistence,
/// <c>Granit.Customers.Endpoints</c> for the admin HTTP API, and
/// <c>Granit.Customers.Privacy</c> for the GDPR export/erasure handlers.
/// </remarks>
public sealed class GranitCustomersModule : GranitModule;
