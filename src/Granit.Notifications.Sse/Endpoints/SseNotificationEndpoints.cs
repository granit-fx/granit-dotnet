using System.Runtime.CompilerServices;
using Granit.Notifications.Sse.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.Sse.Endpoints;

/// <summary>
/// Minimal API endpoints for SSE notification streaming.
/// </summary>
public static class SseNotificationEndpoints
{
    internal const string HeartbeatEventType = "__heartbeat__";

    /// <summary>
    /// Maps the SSE notification stream endpoint at <c>/notifications/stream</c>.
    /// The returned <see cref="RouteGroupBuilder"/> can be further configured by the application
    /// (e.g. <c>.RequireAuthorization()</c>).
    /// </summary>
    public static RouteGroupBuilder MapGranitSseNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGroup("/notifications");

        group.MapGet("/stream", HandleStream)
            .WithName("NotificationSseStream")
            .WithSummary("SSE stream for real-time notifications")
            .ExcludeFromDescription();

        return group;
    }

    private static IResult HandleStream(
        [FromServices] ISseConnectionManager connectionManager,
        [FromServices] IOptions<SseChannelOptions> options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        string? userId = httpContext.User.FindFirst("sub")?.Value
            ?? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (userId is null)
        {
            return TypedResults.Problem(
                detail: "User identifier claim not found.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        int heartbeatSeconds = options.Value.HeartbeatIntervalSeconds;

        return TypedResults.ServerSentEvents(
            StreamNotifications(connectionManager, userId, heartbeatSeconds, cancellationToken));
    }

    private static async IAsyncEnumerable<SseNotificationMessage> StreamNotifications(
        ISseConnectionManager connectionManager,
        string userId,
        int heartbeatSeconds,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        SseConnection connection = connectionManager.Connect(userId);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                bool hasData = await WaitForDataOrHeartbeatAsync(
                    connection, heartbeatSeconds, cancellationToken).ConfigureAwait(false);

                if (hasData)
                {
                    while (connection.Reader.TryRead(out SseNotificationMessage? message))
                    {
                        yield return message;
                    }
                }
                else
                {
                    // Heartbeat timeout — send a sentinel message to prevent proxy disconnection.
                    // Clients should filter messages where NotificationTypeName == "__heartbeat__".
                    yield return new SseNotificationMessage
                    {
                        NotificationTypeName = HeartbeatEventType,
                    };
                }
            }
        }
        finally
        {
            connectionManager.Disconnect(connection);
        }
    }

    /// <summary>
    /// Waits for data on the channel reader or returns false on heartbeat timeout.
    /// Extracted to avoid yield-in-catch restriction.
    /// </summary>
    private static async Task<bool> WaitForDataOrHeartbeatAsync(
        SseConnection connection,
        int heartbeatSeconds,
        CancellationToken cancellationToken)
    {
        using var heartbeatCts =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        heartbeatCts.CancelAfter(TimeSpan.FromSeconds(heartbeatSeconds));

        try
        {
            return await connection.Reader.WaitToReadAsync(heartbeatCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
