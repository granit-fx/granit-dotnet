using Microsoft.EntityFrameworkCore;

namespace Granit.DataExchange.EntityFrameworkCore;

/// <summary>
/// Marker registration declaring a <see cref="DbContext"/> whose entities participate in
/// data exchange (auto-export definition discovery and fallback data-source resolution).
/// </summary>
/// <remarks>
/// Registered via <c>AddDataExchangeDbContext&lt;TContext&gt;()</c>. Discovery is strictly
/// registration-based: contexts that are not declared are invisible to auto-export and to the
/// fallback <c>IExportDataSource&lt;T&gt;</c>, which keeps both discovery paths symmetric and
/// deterministic (no assembly scanning).
/// </remarks>
/// <param name="ContextType">The concrete <see cref="DbContext"/> subclass.</param>
public sealed record DataExchangeDbContextRegistration(Type ContextType);
