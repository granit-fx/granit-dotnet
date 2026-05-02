using Granit.Identity;
using Granit.Modularity;
using Granit.Parties.Identity.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Parties.Identity;

/// <summary>
/// Granit module that wires Granit.Parties into Granit.Identity per
/// ADR-051 B-step 5. Two Wolverine handlers
/// (<see cref="Handlers.EnsurePartyForUserHandler"/> and
/// <see cref="Handlers.SyncProfileToPartyHandler"/>) are auto-discovered
/// and keep a <see cref="Granit.Parties.Domain.Party"/> of kind
/// <see cref="Granit.Parties.Domain.PartyKind.Individual"/> in sync with
/// every canonical <see cref="Granit.Identity.Domain.User"/>.
/// </summary>
/// <remarks>
/// <para>
/// Kept separate from <c>Granit.Parties</c> so apps that do not load
/// <c>Granit.Identity</c> (rare — Identity is foundation but the bridge
/// only exists when both modules ship together) do not inherit a
/// dependency on it. Lives in the <c>Parties</c> tier (consumer side)
/// per the <c>Granit.Parties.MultiTenancy</c> precedent — the bridge
/// owns the Party-creation logic, not the Identity module.
/// </para>
/// <para>
/// Bridge contract per ADR-051:
/// </para>
/// <list type="bullet">
///   <item><description><c>UserCreatedEto</c> → ensure a Party of kind Individual exists for the user (idempotent).</description></item>
///   <item><description><c>UserProfileChangedEto</c> → propagate name / locale / timezone changes to the Party.</description></item>
///   <item><description>No reverse sync (Party → User) — the canonical User aggregate is the source of truth for identity-side fields.</description></item>
/// </list>
/// </remarks>
[DependsOn(
    typeof(GranitIdentityModule),
    typeof(GranitPartiesModule))]
public sealed class GranitPartiesIdentityModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddOptions<GranitPartiesIdentityOptions>()
            .BindConfiguration(GranitPartiesIdentityOptions.SectionName)
            .Validate(o => o.DefaultCurrency is { Length: 3 },
                $"{GranitPartiesIdentityOptions.SectionName}:DefaultCurrency must be a 3-letter ISO 4217 code.")
            .ValidateOnStart();
    }
}
