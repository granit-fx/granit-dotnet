using System.ComponentModel.DataAnnotations;
using Wolverine.Persistence;

namespace Granit.Wolverine.SqlServer.Options;

/// <summary>
/// Configuration options for the SQL Server Wolverine provider.
/// Bound from the <c>"Wolverine:SqlServer"</c> section of <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>TransportConnectionString</c> — the SQL Server connection string for the transactional outbox.
/// </para>
/// <para>
/// <c>TransactionMode</c> defaults to <see cref="TransactionMiddlewareMode.Eager"/>
/// (explicit <c>BeginTransactionAsync</c>) to satisfy ISO 27001 audit requirements.
/// Use <see cref="TransactionMiddlewareMode.Lightweight"/> only for non-critical background
/// processes where <c>SaveChangesAsync</c>-level isolation is sufficient.
/// </para>
/// </remarks>
public sealed class WolverineSqlServerOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Wolverine:SqlServer";

    /// <summary>
    /// SQL Server connection string for the Wolverine Outbox tables.
    /// Must be non-empty. Required for ISO 27001-compliant durable messaging.
    /// </summary>
    [Required]
    public string TransportConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// EF Core transaction wrapping mode for Wolverine handlers.
    /// Default: <see cref="TransactionMiddlewareMode.Eager"/> (ISO 27001-recommended).
    /// </summary>
    public TransactionMiddlewareMode TransactionMode { get; set; } = TransactionMiddlewareMode.Eager;
}
