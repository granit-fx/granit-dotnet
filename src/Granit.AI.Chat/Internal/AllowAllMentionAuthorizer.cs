using Granit.AI.Chat.Mentions;

namespace Granit.AI.Chat.Internal;

/// <summary>
/// Default <see cref="IAIMentionAuthorizer"/>: allows every resolver. The base package has no
/// permission system, so without a host-supplied authorizer mention types declaring a
/// <see cref="IAIMentionResolver.RequiredPermission"/> would be unenforceable. The HTTP layer
/// replaces this with a permission-bound authorizer.
/// </summary>
internal sealed class AllowAllMentionAuthorizer : IAIMentionAuthorizer
{
    public ValueTask<bool> IsAuthorizedAsync(IAIMentionResolver resolver, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(true);
}
