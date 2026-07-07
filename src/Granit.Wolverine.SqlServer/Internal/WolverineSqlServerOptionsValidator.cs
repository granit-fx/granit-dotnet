using Granit.Wolverine.SqlServer.Options;
using Microsoft.Extensions.Options;

namespace Granit.Wolverine.SqlServer.Internal;

/// <summary>
/// Validates <see cref="WolverineSqlServerOptions"/> at startup.
/// </summary>
internal sealed class WolverineSqlServerOptionsValidator : IValidateOptions<WolverineSqlServerOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, WolverineSqlServerOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.TransportConnectionString) &&
            string.IsNullOrWhiteSpace(options.TransportConnectionStringName))
        {
            return ValidateOptionsResult.Fail(
                $"Either {nameof(options.TransportConnectionString)} or {nameof(options.TransportConnectionStringName)} " +
                "must be configured. A valid SQL Server connection string is required for the Wolverine Outbox (ISO 27001 compliance). " +
                "Use TransportConnectionStringName for Aspire integration (e.g., \"catalog-db\").");
        }

        return ValidateOptionsResult.Success;
    }
}
