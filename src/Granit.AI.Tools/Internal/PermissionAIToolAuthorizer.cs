using Granit.Authorization;

namespace Granit.AI.Tools.Internal;

/// <summary>
/// Default <see cref="IAIToolAuthorizer"/>: keeps every ungated tool and every gated tool whose
/// <see cref="IGatedAITool.RequiredPermission"/> is granted to the current caller, resolved via
/// <see cref="IPermissionChecker"/> (one batched check for all gated permissions).
/// </summary>
internal sealed class PermissionAIToolAuthorizer(IPermissionChecker permissionChecker) : IAIToolAuthorizer
{
    public async Task<IReadOnlyList<IAITool>> FilterAuthorizedAsync(
        IReadOnlyList<IAITool> tools,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tools);

        List<string> gatedPermissions = [.. tools
            .OfType<IGatedAITool>()
            .Select(t => t.RequiredPermission)
            .Distinct(StringComparer.Ordinal)];

        if (gatedPermissions.Count == 0)
        {
            return tools;
        }

        IReadOnlyList<string> granted = await permissionChecker
            .GetGrantedAsync(gatedPermissions, cancellationToken)
            .ConfigureAwait(false);
        HashSet<string> grantedSet = [.. granted];

        return [.. tools.Where(tool =>
            tool is not IGatedAITool gated || grantedSet.Contains(gated.RequiredPermission))];
    }
}
