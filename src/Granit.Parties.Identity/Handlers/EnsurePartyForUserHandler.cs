using System.Diagnostics.CodeAnalysis;
using Granit.Guids;
using Granit.Identity.Events;
using Granit.Parties.Domain;
using Granit.Parties.Identity.Options;
using Microsoft.Extensions.Options;

namespace Granit.Parties.Identity.Handlers;

/// <summary>
/// Wolverine-discovered handler that materialises a
/// <see cref="Party"/> of kind <see cref="PartyKind.Individual"/> for
/// every canonical <see cref="Granit.Identity.Domain.User"/> the
/// moment it is created. Idempotent — replays of the same
/// <see cref="UserCreatedEto"/> return without inserting a duplicate
/// Party because the lookup keys on <see cref="Party.UserId"/>.
/// </summary>
/// <remarks>
/// The handler does not copy email or phone into the Party on
/// creation. Those fields move on profile changes via
/// <see cref="SyncProfileToPartyHandler"/> only when admin curates
/// them — surfacing PII into the CRM-side Party records is opt-in,
/// not implicit, so a tenant that uses Identity for auth-only stays
/// auth-only on the Party side.
/// </remarks>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public class EnsurePartyForUserHandler
{
    public static async Task HandleAsync(
        UserCreatedEto @event,
        IPartyReader reader,
        IPartyWriter writer,
        IGuidGenerator guidGenerator,
        IOptions<GranitPartiesIdentityOptions> options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);

        // Idempotency: at-least-once Wolverine delivery means this
        // handler may run twice for the same UserCreatedEto. The Party
        // table has no DB-level unique on UserId for Individuals, so
        // the dedup is in code — same shape as the
        // SeedDefaultPartyOnTenantCreatedHandler precedent.
        Party? existing = await reader.GetByUserIdAsync(@event.UserId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return;
        }

        var party = Party.Create(
            id: guidGenerator.Create(),
            tenantId: @event.TenantId,
            kind: PartyKind.Individual,
            name: @event.DisplayName,
            defaultCurrency: options.Value.DefaultCurrency);

        party.LinkToUser(@event.UserId);

        await writer.AddAsync(party, cancellationToken).ConfigureAwait(false);
    }
}
