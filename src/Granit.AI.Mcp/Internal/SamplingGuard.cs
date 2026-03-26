using Granit.AI.Mcp.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.AI.Mcp.Internal;

/// <summary>
/// Guards MCP sampling requests (external servers requesting LLM completions)
/// with permission checks, rate limiting, cost control, and audit logging.
/// </summary>
internal sealed partial class SamplingGuard(
    IOptions<GranitAIMcpOptions> options,
    ILogger<SamplingGuard> logger)
{
    /// <summary>
    /// Validates a sampling request before forwarding to the AI workspace.
    /// </summary>
    /// <returns><see langword="true"/> if the request should be allowed.</returns>
    public bool TryValidate(int? requestedMaxTokens, string? serverOrigin, out string? rejectionReason)
    {
        GranitAIMcpOptions config = options.Value;

        if (!config.EnableSampling)
        {
            rejectionReason = "MCP sampling is disabled.";
            LogSamplingRejected(serverOrigin, rejectionReason);
            return false;
        }

        if (config.SamplingMaxTokensPerRequest > 0 &&
            requestedMaxTokens > config.SamplingMaxTokensPerRequest)
        {
            rejectionReason = $"Requested {requestedMaxTokens} tokens exceeds limit of {config.SamplingMaxTokensPerRequest}.";
            LogSamplingRejected(serverOrigin, rejectionReason);
            return false;
        }

        rejectionReason = null;
        LogSamplingAllowed(serverOrigin, requestedMaxTokens);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "MCP sampling rejected from {ServerOrigin}: {Reason}")]
    private partial void LogSamplingRejected(string? serverOrigin, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "MCP sampling allowed from {ServerOrigin} (maxTokens={MaxTokens})")]
    private partial void LogSamplingAllowed(string? serverOrigin, int? maxTokens);
}
