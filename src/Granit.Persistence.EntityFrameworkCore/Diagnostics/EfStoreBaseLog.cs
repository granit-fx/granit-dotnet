using Microsoft.Extensions.Logging;

namespace Granit.Persistence.EntityFrameworkCore.Diagnostics;

/// <summary>
/// Source-generated log messages for <see cref="EfStoreBase{TEntity, TContext}"/>.
/// Lives in a non-generic host class because <c>[LoggerMessage]</c> source generation
/// does not support generic containing types.
/// </summary>
internal static partial class EfStoreBaseLog
{
    [LoggerMessage(
        EventId = 8102,
        Level = LogLevel.Information,
        Message = "Explicit cross-tenant query on {Entity} from {CallerMember} ({CallerFile}:{CallerLine}).")]
    public static partial void ExplicitCrossTenantQuery(
        ILogger logger, string entity, string callerMember, string callerFile, int callerLine);
}
