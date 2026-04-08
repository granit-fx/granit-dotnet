using System.ComponentModel.DataAnnotations;
using Wolverine.Persistence;

namespace Granit.Wolverine.Postgresql.Options;

/// <summary>
/// Configuration options for the PostgreSQL Wolverine provider.
/// Bound from the <c>"WolverinePostgresql"</c> section of <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>TransportConnectionString</c> — the PostgreSQL connection string for the transactional outbox.
/// </para>
/// <para>
/// <c>TransactionMode</c> defaults to <see cref="TransactionMiddlewareMode.Eager"/>
/// (explicit <c>BeginTransactionAsync</c>) to satisfy ISO 27001 audit requirements.
/// Use <see cref="TransactionMiddlewareMode.Lightweight"/> only for non-critical background
/// processes where <c>SaveChangesAsync</c>-level isolation is sufficient.
/// </para>
/// </remarks>
public sealed class WolverinePostgresqlOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "WolverinePostgresql";

    /// <summary>
    /// Explicit PostgreSQL connection string for the Wolverine Outbox tables.
    /// Takes priority over <see cref="TransportConnectionStringName"/>.
    /// Required (either this or <see cref="TransportConnectionStringName"/>) for ISO 27001-compliant durable messaging.
    /// </summary>
    public string? TransportConnectionString { get; set; }

    /// <summary>
    /// Name of the connection string in the <c>ConnectionStrings</c> configuration section.
    /// Used as fallback when <see cref="TransportConnectionString"/> is null or empty.
    /// Enables seamless integration with .NET Aspire service discovery.
    /// </summary>
    /// <example>
    /// <code>
    /// // appsettings.json
    /// {
    ///   "WolverinePostgresql": {
    ///     "TransportConnectionStringName": "catalog-db"
    ///   }
    /// }
    /// // Aspire injects ConnectionStrings:catalog-db → Wolverine reads it automatically.
    /// </code>
    /// </example>
    public string? TransportConnectionStringName { get; set; }

    /// <summary>
    /// EF Core transaction wrapping mode for Wolverine handlers.
    /// Default: <see cref="TransactionMiddlewareMode.Eager"/> (ISO 27001-recommended).
    /// </summary>
    public TransactionMiddlewareMode TransactionMode { get; set; } = TransactionMiddlewareMode.Eager;

    /// <summary>
    /// PostgreSQL schema for Wolverine envelope tables (inbox, outbox, dead letter).
    /// Default: <c>null</c> — falls back to <see cref="Granit.Persistence.EntityFrameworkCore.GranitDbDefaults.HostDbSchema"/>,
    /// then Wolverine's built-in default (<c>"wolverine"</c>).
    /// </summary>
    /// <remarks>
    /// <para><b>SharedDatabase</b>: leave <c>null</c> — Wolverine uses <c>"wolverine"</c> schema.</para>
    /// <para><b>SchemaPerTenant</b>: automatically uses <c>HostDbSchema</c> (e.g. <c>"host"</c>) so
    /// Wolverine tables live alongside other host infrastructure.</para>
    /// <para><b>DatabasePerTenant</b>: leave <c>null</c> — Wolverine uses the host database.</para>
    /// </remarks>
    public string? SchemaName { get; set; }
}
