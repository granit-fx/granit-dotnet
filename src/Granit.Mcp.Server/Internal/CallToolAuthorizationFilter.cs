using Granit.Authorization;
using Granit.Mcp.Server.Permissions;
using Granit.MultiTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Granit.Mcp.Server.Internal;

/// <summary>
/// Default-deny authorization gate for MCP <c>tools/call</c>. Runs before dispatch and
/// before the output sanitizers, and can reject an invocation without calling <c>next</c>.
/// </summary>
/// <remarks>
/// <para>
/// The SDK's own call-tool filter (registered by <c>AddAuthorizationFilters()</c>) only
/// enforces tools that carry explicit authorization metadata (<see cref="IAuthorizeData"/>,
/// <see cref="AuthorizationPolicy"/>, <see cref="IAuthorizationRequirementData"/>); an
/// un-annotated tool is dispatched to any principal that reached the server — a fail-open.
/// This filter closes that gap:
/// </para>
/// <list type="number">
/// <item>An un-resolvable tool is rejected (fail-closed).</item>
/// <item>A tool declaring <c>[McpTenantScope(RequireTenant = true)]</c> is rejected when no
/// tenant context is active, re-running the <c>tools/list</c> visibility check at call time.</item>
/// <item>A tool carrying explicit authorization metadata is passed through — the SDK filter
/// enforces its policy, so this filter never double-enforces it.</item>
/// <item>Any other tool must satisfy the coarse <see cref="McpPermissions.Tools.Execute"/>
/// fallback permission via <see cref="IPermissionChecker"/>; otherwise it is rejected.</item>
/// </list>
/// <para>
/// Lives in <c>Granit.Mcp.Server</c> rather than the transport-agnostic base
/// <c>Granit.Mcp</c> because the permission fallback needs <see cref="IPermissionChecker"/>
/// from <c>Granit.Authorization</c> — a server-tier dependency the base module must not
/// acquire (stdio hosts without an HTTP principal have no RBAC pipeline to consult).
/// </para>
/// </remarks>
internal static partial class CallToolAuthorizationFilter
{
    /// <summary>
    /// Wraps the call-tool handler with the default-deny gate.
    /// </summary>
    public static McpRequestHandler<CallToolRequestParams, CallToolResult> Wrap(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next) =>
        async (context, ct) =>
        {
            CallToolResult? denial = await EvaluateAsync(context, ct).ConfigureAwait(false);
            return denial ?? await next(context, ct).ConfigureAwait(false);
        };

    /// <summary>
    /// Thin adapter over <see cref="EvaluateAsync(IServiceProvider?, string, IReadOnlyList{object}?, CancellationToken)"/>
    /// that pulls the tool name and matched-primitive metadata off the SDK request context.
    /// </summary>
    private static ValueTask<CallToolResult?> EvaluateAsync(
        RequestContext<CallToolRequestParams> context,
        CancellationToken ct) =>
        EvaluateAsync(
            context.Services,
            context.Params?.Name ?? "unknown",
            context.MatchedPrimitive?.Metadata,
            ct);

    /// <summary>
    /// Returns a non-<see langword="null"/> error result when the invocation must be denied,
    /// or <see langword="null"/> when it may proceed to dispatch. Exposed for isolated testing.
    /// </summary>
    internal static async ValueTask<CallToolResult?> EvaluateAsync(
        IServiceProvider? services,
        string toolName,
        IReadOnlyList<object>? matchedPrimitiveMetadata,
        CancellationToken ct)
    {
        ILogger? logger = services?
            .GetService<ILoggerFactory>()?
            .CreateLogger("Granit.Mcp.Server.CallToolAuthorization");

        // Fail closed: a tool we cannot resolve to a CLR type cannot be reasoned about.
        McpToolTypeRegistry? registry = services?.GetService<McpToolTypeRegistry>();
        Type? toolType = registry?.Resolve(toolName);
        if (toolType is null)
        {
            if (logger is not null)
            {
                LogUnresolvedTool(logger, toolName);
            }

            return Deny();
        }

        // Re-run the tenant-scope check at call time (tools/list only hides, it does not gate).
        if (RequiresTenant(toolType))
        {
            ICurrentTenant? currentTenant = services?.GetService<ICurrentTenant>();
            if (currentTenant is not { IsAvailable: true })
            {
                if (logger is not null)
                {
                    LogTenantRequired(logger, toolName);
                }

                return Deny();
            }
        }

        // A tool carrying explicit authorization metadata is enforced by the SDK's own
        // call-tool filter — passing through here avoids double-enforcement.
        if (HasExplicitAuthorization(matchedPrimitiveMetadata))
        {
            return null;
        }

        // Default-deny fallback: the principal must hold the coarse execute permission.
        IPermissionChecker? permissionChecker = services?.GetService<IPermissionChecker>();
        if (permissionChecker is null)
        {
            if (logger is not null)
            {
                LogNoPermissionChecker(logger, toolName);
            }

            return Deny();
        }

        bool granted = await permissionChecker
            .IsGrantedAsync(McpPermissions.Tools.Execute, ct)
            .ConfigureAwait(false);

        if (!granted)
        {
            if (logger is not null)
            {
                LogExecuteDenied(logger, toolName);
            }

            return Deny();
        }

        return null;
    }

    private static bool RequiresTenant(Type toolType) =>
        toolType
            .GetCustomAttributes(typeof(McpTenantScopeAttribute), inherit: false)
            .OfType<McpTenantScopeAttribute>()
            .Any(scope => scope.RequireTenant);

    // Mirrors ModelContextProtocol.AspNetCore's HasAuthorizationMetadata: explicit opt-out
    // (IAllowAnonymous) or any positive authorization datum counts as an explicit decision.
    private static bool HasExplicitAuthorization(IReadOnlyList<object>? metadata)
    {
        if (metadata is null)
        {
            return false;
        }

        if (metadata.Any(m => m is IAllowAnonymous))
        {
            return true;
        }

        return metadata.Any(m =>
            m is IAuthorizeData or AuthorizationPolicy or IAuthorizationRequirementData);
    }

    private static CallToolResult Deny() => new()
    {
        IsError = true,
        Content =
        [
            new TextContentBlock
            {
                Text = "Access forbidden: this tool requires explicit authorization "
                    + "or the 'Mcp.Tools.Execute' permission.",
            },
        ],
    };

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "MCP tools/call denied: tool '{ToolName}' could not be resolved to a CLR type.")]
    private static partial void LogUnresolvedTool(ILogger logger, string toolName);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "MCP tools/call denied: tool '{ToolName}' requires a tenant context but none is active.")]
    private static partial void LogTenantRequired(ILogger logger, string toolName);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "MCP tools/call denied: no IPermissionChecker registered to evaluate the "
            + "'Mcp.Tools.Execute' fallback for tool '{ToolName}'.")]
    private static partial void LogNoPermissionChecker(ILogger logger, string toolName);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "MCP tools/call denied: principal lacks 'Mcp.Tools.Execute' for un-annotated tool '{ToolName}'.")]
    private static partial void LogExecuteDenied(ILogger logger, string toolName);
}
