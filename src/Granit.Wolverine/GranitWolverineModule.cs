using Granit.Modularity;
using Granit.Users;
using Granit.Validation;
using Granit.Wolverine.Extensions;

namespace Granit.Wolverine;

/// <summary>
/// Granit module for Wolverine messaging (provider-agnostic core).
/// </summary>
/// <remarks>
/// This module configures the core Wolverine infrastructure: IDomainEvent local routing
/// and context behavior registration. It does not configure any persistence or Outbox.
/// <para>
/// For durable messaging, add a provider module after this one:
/// <list type="bullet">
///   <item><c>GranitWolverinePostgresqlModule</c> — PostgreSQL Outbox (ISO 27001)</item>
/// </list>
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitValidationModule))]
public sealed class GranitWolverineModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitWolverine();
}
