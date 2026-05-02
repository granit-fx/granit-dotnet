using System.Diagnostics.CodeAnalysis;
using Granit.Identity.Events;
using Granit.Parties.Domain;

namespace Granit.Parties.Identity.Handlers;

/// <summary>
/// Wolverine-discovered handler that propagates
/// <see cref="UserProfileChangedEto"/> changes onto the matching
/// <see cref="Party"/> through <see cref="Party.UpdateIdentity"/>.
/// </summary>
/// <remarks>
/// <para>
/// No-op when no Party is linked yet — that case happens during the
/// brief window between a User insert and the
/// <see cref="EnsurePartyForUserHandler"/> processing the
/// corresponding <see cref="UserCreatedEto"/>. The next profile
/// change after the Party exists picks it up.
/// </para>
/// <para>
/// Sync direction is one-way (User → Party) per ADR-051. Reverse
/// sync (Party → User) is intentionally out of scope: the canonical
/// User aggregate is the source of truth for identity-side fields.
/// </para>
/// </remarks>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public class SyncProfileToPartyHandler
{
    public static async Task HandleAsync(
        UserProfileChangedEto @event,
        IPartyReader reader,
        IPartyWriter writer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);

        Party? party = await reader.GetByUserIdAsync(@event.UserId, cancellationToken).ConfigureAwait(false);
        if (party is null)
        {
            // Race window: the EnsurePartyForUserHandler hasn't run
            // yet (or failed). Nothing to sync — the next profile
            // change will catch up once the Party exists.
            return;
        }

        party.UpdateIdentity(
            name: @event.DisplayName,
            language: @event.PreferredLocale,
            timezone: @event.Timezone);

        await writer.UpdateAsync(party, cancellationToken).ConfigureAwait(false);
    }
}
