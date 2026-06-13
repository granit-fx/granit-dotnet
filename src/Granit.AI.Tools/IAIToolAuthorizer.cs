namespace Granit.AI.Tools;

/// <summary>
/// Filters a set of tools down to those the current caller is allowed to use, enforcing the
/// per-tool permission gate (<see cref="IGatedAITool"/>). Ungated tools always pass.
/// </summary>
public interface IAIToolAuthorizer
{
    /// <summary>Returns the subset of <paramref name="tools"/> available to the current caller.</summary>
    Task<IReadOnlyList<IAITool>> FilterAuthorizedAsync(
        IReadOnlyList<IAITool> tools,
        CancellationToken cancellationToken = default);
}
