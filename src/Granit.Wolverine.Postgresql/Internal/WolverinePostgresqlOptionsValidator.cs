using Granit.Wolverine.Postgresql.Options;
using Microsoft.Extensions.Options;

namespace Granit.Wolverine.Postgresql.Internal;

/// <summary>
/// Validates <see cref="WolverinePostgresqlOptions"/> at startup.
/// </summary>
internal sealed class WolverinePostgresqlOptionsValidator : IValidateOptions<WolverinePostgresqlOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, WolverinePostgresqlOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.TransportConnectionString) &&
            string.IsNullOrWhiteSpace(options.TransportConnectionStringName))
        {
            return ValidateOptionsResult.Fail(
                $"Either {nameof(options.TransportConnectionString)} or {nameof(options.TransportConnectionStringName)} " +
                "must be configured. A valid PostgreSQL connection string is required for the Wolverine Outbox (ISO 27001 compliance). " +
                "Use TransportConnectionStringName for Aspire integration (e.g., \"catalog-db\").");
        }

        return ValidateOptionsResult.Success;
    }
}
