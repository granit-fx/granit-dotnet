using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Granit.Http.Idempotency.Abstractions;
using Granit.Http.Idempotency.Models;
using Granit.MultiTenancy;
using Granit.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IO;

namespace Granit.Http.Idempotency.Internal;

/// <summary>
/// ASP.NET Core middleware implementing HTTP idempotency (Stripe-style).
/// </summary>
/// <remarks>
/// State machine: <c>Absent → InProgress → Completed</c> (or <c>Absent</c> on 5xx/timeout).
/// Uses <see cref="IMiddleware"/> so scoped services (<see cref="ICurrentUserService"/>,
/// <see cref="ICurrentTenant"/>) are resolved per-request from the DI request scope.
/// </remarks>
internal sealed partial class IdempotencyMiddleware(
    IOptions<IdempotencyOptions> options,
    IIdempotencyStore store,
    RecyclableMemoryStreamManager streamManager,
    TimeProvider timeProvider,
    ICurrentUserService currentUser,
    ICurrentTenant currentTenant,
    ILogger<IdempotencyMiddleware> logger) : IMiddleware
{
    private readonly IdempotencyOptions _opts = options.Value;
    private readonly IIdempotencyStore _store = store;
    private readonly RecyclableMemoryStreamManager _streamManager = streamManager;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ICurrentUserService _currentUser = currentUser;
    private readonly ICurrentTenant _currentTenant = currentTenant;
    private readonly ILogger<IdempotencyMiddleware> _logger = logger;

    /// <inheritdoc/>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // 1. Resolve endpoint metadata — bypass if endpoint is not decorated
        IIdempotencyMetadata? meta = context.GetEndpoint()?.Metadata.GetMetadata<IIdempotencyMetadata>();
        if (meta is null)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        // 2. Read the idempotency key header
        string? idempotencyKey = context.Request.Headers[_opts.HeaderName];
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            if (!meta.Required)
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            await WriteProblemAsync(context, StatusCodes.Status422UnprocessableEntity,
                "Missing Idempotency-Key",
                $"The '{_opts.HeaderName}' header is required for this endpoint.").ConfigureAwait(false);
            return;
        }

        // 2b. Reject oversized keys before any hashing or cache lookup. Bounds
        // the work a single client can force per request; Kestrel's global
        // header limit is a coarser, pipeline-wide guard.
        if (idempotencyKey.Length > _opts.MaxKeyLength)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest,
                "Idempotency Key Too Long",
                $"The '{_opts.HeaderName}' header value exceeds the maximum allowed length " +
                $"of {_opts.MaxKeyLength} characters.").ConfigureAwait(false);
            return;
        }

        // 3. Reject multipart — boundaries are non-deterministic, hashing is unreliable
        if (context.Request.HasFormContentType)
        {
            await WriteProblemAsync(context, StatusCodes.Status422UnprocessableEntity,
                "Unsupported Content-Type",
                "Multipart/form-data requests cannot be made idempotent due to non-deterministic boundaries.").ConfigureAwait(false);
            return;
        }

        // 4. Compute payload hash once (reads + rewinds the buffered body)
        string payloadHash = await ComputePayloadHashAsync(context, idempotencyKey).ConfigureAwait(false);

        // 5. Build the composite Redis key
        string redisKey = BuildRedisKey(context, idempotencyKey);

        // 6. Check for an existing entry
        IdempotencyEntry? existing = await _store.GetAsync(redisKey, context.RequestAborted).ConfigureAwait(false);
        if (existing is not null)
        {
            await HandleExistingEntryAsync(context, existing, redisKey, payloadHash, meta).ConfigureAwait(false);
            return;
        }

        // 7. Acquire the InProgress lock (SET NX PX — atomic)
        DateTimeOffset createdAt = _timeProvider.GetUtcNow();
        IdempotencyEntry inProgressEntry = new()
        {
            State = IdempotencyState.InProgress,
            PayloadHash = payloadHash,
            CreatedAt = createdAt,
        };

        bool acquired = await _store.TryAcquireAsync(redisKey, inProgressEntry, _opts.InProgressTtl, context.RequestAborted).ConfigureAwait(false);
        if (!acquired)
        {
            // Another pod acquired between GetAsync and TryAcquireAsync — re-read
            IdempotencyEntry? concurrent = await _store.GetAsync(redisKey, context.RequestAborted).ConfigureAwait(false);
            if (concurrent is not null)
            {
                await HandleExistingEntryAsync(context, concurrent, redisKey, payloadHash, meta).ConfigureAwait(false);
                return;
            }

            // Extreme TTL race: key vanished between acquire failure and re-read.
            // We DO NOT proceed without a lock — doing so would allow two pods to
            // execute the same idempotent request concurrently (double payment,
            // double email, etc.) the instant the lock's TTL ticks over. Instead,
            // return 409 + Retry-After so the client retries in a window where
            // we can re-acquire cleanly. Preserves the at-most-once guarantee.
            int retryAfter = (int)_opts.InProgressTtl.TotalSeconds;
            context.Response.Headers.RetryAfter = retryAfter.ToString();
            LogRaceCondition(_logger, redisKey);
            await WriteProblemAsync(context, StatusCodes.Status409Conflict,
                "Idempotency Race",
                $"Could not acquire the idempotency lock for this request. Retry after {retryAfter}s.").ConfigureAwait(false);
            return;
        }

        // 8. Execute downstream handler and capture the response
        await ExecuteAndCaptureAsync(context, next, redisKey, payloadHash, createdAt, meta).ConfigureAwait(false);
    }

    // =========================================================================
    // Existing entry handling
    // =========================================================================

    private async Task HandleExistingEntryAsync(
        HttpContext context,
        IdempotencyEntry entry,
        string redisKey,
        string payloadHash,
        IIdempotencyMetadata meta)
    {
        if (entry.State == IdempotencyState.InProgress)
        {
            int retryAfter = (int)_opts.InProgressTtl.TotalSeconds;
            context.Response.Headers.RetryAfter = retryAfter.ToString();
            await WriteProblemAsync(context, StatusCodes.Status409Conflict,
                "Request In Progress",
                $"A request with this idempotency key is already being processed. Retry after {retryAfter}s.").ConfigureAwait(false);
            return;
        }

        // Validate payload hash to detect request mutation (prevents key reuse with different body)
        if (!string.Equals(entry.PayloadHash, payloadHash, StringComparison.Ordinal))
        {
            await WriteProblemAsync(context, StatusCodes.Status422UnprocessableEntity,
                "Idempotency Key Conflict",
                "The request payload does not match the original request associated with this idempotency key.").ConfigureAwait(false);
            return;
        }

        // Tombstoned entry: the request ran successfully once but its response
        // is not replayable. Preserve the at-most-once guarantee by returning
        // an explicit failure instead of re-executing the handler.
        if (entry.State == IdempotencyState.Tombstoned)
        {
            int statusCode = entry.TombstoneReason switch
            {
                IdempotencyTombstoneReason.ResponseTooLarge => StatusCodes.Status413PayloadTooLarge,
                _ => StatusCodes.Status500InternalServerError,
            };
            context.Response.Headers["X-Idempotency-Tombstone"] =
                entry.TombstoneReason?.ToString() ?? "Unknown";
            LogTombstoneReplay(_logger, redisKey, entry.TombstoneReason);
            await WriteProblemAsync(context, statusCode,
                "Idempotent Response Not Replayable",
                "The original response for this idempotency key cannot be replayed. " +
                "The request was executed successfully but its response was not cached " +
                "(e.g. exceeded the replay size limit). Use a new idempotency key to retry.").ConfigureAwait(false);
            return;
        }

        // Replay the completed response
        await ReplayResponseAsync(context, entry, _opts).ConfigureAwait(false);
        LogReplay(_logger, redisKey, entry.StatusCode, entry.CompletedAt);

        // meta may carry per-endpoint TTL overrides — kept as parameter for symmetry
        _ = meta;
    }

    // =========================================================================
    // Execute and capture
    // =========================================================================

    private async Task ExecuteAndCaptureAsync(
        HttpContext context,
        RequestDelegate next,
        string redisKey,
        string payloadHash,
        DateTimeOffset createdAt,
        IIdempotencyMetadata meta)
    {
        Stream originalBody = context.Response.Body;
        CancellationToken originalAborted = context.RequestAborted;

        await using RecyclableMemoryStream captureStream = _streamManager.GetStream("idempotency");
        context.Response.Body = captureStream;

        using CancellationTokenSource timeoutCts = new(_opts.ExecutionTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            originalAborted, timeoutCts.Token);
        context.RequestAborted = linkedCts.Token;

        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !originalAborted.IsCancellationRequested)
        {
            // ExecutionTimeout fired: release InProgress lock and write 503 so clients can retry
            await _store.DeleteAsync(redisKey, CancellationToken.None).ConfigureAwait(false);
            LogExecutionTimeout(_logger, redisKey, (int)_opts.ExecutionTimeout.TotalSeconds);

            // Restore before WriteProblemAsync so 503 goes to the client socket, not the capture stream
            context.Response.Body = originalBody;
            context.RequestAborted = originalAborted;

            await WriteProblemAsync(context, StatusCodes.Status503ServiceUnavailable,
                "Execution Timeout",
                $"The request handler exceeded the maximum allowed execution time of {(int)_opts.ExecutionTimeout.TotalSeconds}s. Please retry.").ConfigureAwait(false);
            return; // finally restores context body; pipeline does not continue
        }
        catch (OperationCanceledException) when (originalAborted.IsCancellationRequested)
        {
            // HTTP 499: client disconnected — never cache a potentially truncated response
            LogClientDisconnected(_logger, redisKey, (int)_opts.InProgressTtl.TotalSeconds);
            throw;
        }
        catch
        {
            // 5xx or unexpected exception — release the InProgress lock
            await _store.DeleteAsync(redisKey, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        finally
        {
            context.Response.Body = originalBody;
            context.RequestAborted = originalAborted;
        }

        // Copy captured bytes to the actual response body
        captureStream.Seek(0, SeekOrigin.Begin);
        await captureStream.CopyToAsync(originalBody, originalAborted).ConfigureAwait(false);

        // Decide whether to cache the response
        int statusCode = context.Response.StatusCode;
        if (!_opts.ShouldCacheStatusCode(statusCode))
        {
            await _store.DeleteAsync(redisKey, CancellationToken.None).ConfigureAwait(false);
            return;
        }

        DateTimeOffset completedAt = _timeProvider.GetUtcNow();

        // Response body too large to cache: store a tombstone so retries see
        // an explicit "executed once, not replayable" state instead of re-
        // executing the business handler. The original client has already
        // received their full response at this point.
        if (captureStream.Length > _opts.MaxResponseSizeBytes)
        {
            IdempotencyEntry tombstoneEntry = new()
            {
                State = IdempotencyState.Tombstoned,
                PayloadHash = payloadHash,
                CreatedAt = createdAt,
                StatusCode = statusCode,
                CompletedAt = completedAt,
                TombstoneReason = IdempotencyTombstoneReason.ResponseTooLarge,
            };

            LogResponseTooLarge(_logger, redisKey, captureStream.Length, _opts.MaxResponseSizeBytes);
            await _store.SetCompletedAsync(redisKey, tombstoneEntry, _opts.TombstoneTtl, CancellationToken.None).ConfigureAwait(false);
            return;
        }

        // Capture response headers, filtering anything that must not be
        // replayed to a later retry (Set-Cookie rotation, WWW-Authenticate
        // challenges, etc. — see IdempotencyOptions.ExcludedResponseHeaders).
        Dictionary<string, string[]> headers = [];
        foreach (System.Collections.Generic.KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> h in context.Response.Headers)
        {
            if (_opts.ExcludedResponseHeaders.Contains(h.Key))
            {
                continue;
            }

            // HTTP header values are never null — null-suppression is intentional
            string[] values = h.Value.ToArray()!;
            if (values.Length > 0)
            {
                headers[h.Key] = values;
            }
        }

        byte[] responseBody = captureStream.GetReadOnlySequence().ToArray();

        TimeSpan completedTtl = meta.CompletedTtlSeconds > 0
            ? TimeSpan.FromSeconds(meta.CompletedTtlSeconds)
            : _opts.CompletedTtl;

        IdempotencyEntry completedEntry = new()
        {
            State = IdempotencyState.Completed,
            PayloadHash = payloadHash,
            CreatedAt = createdAt,
            StatusCode = statusCode,
            ResponseHeaders = headers.Count > 0 ? headers : null,
            ResponseBody = responseBody.Length > 0 ? responseBody : null,
            CompletedAt = completedAt,
        };

        await _store.SetCompletedAsync(redisKey, completedEntry, completedTtl, CancellationToken.None).ConfigureAwait(false);
    }

    // =========================================================================
    // Response replay
    // =========================================================================

    private static async Task ReplayResponseAsync(HttpContext context, IdempotencyEntry entry, IdempotencyOptions opts)
    {
        context.Response.StatusCode = entry.StatusCode!.Value;

        if (entry.ResponseHeaders is not null)
        {
            foreach (System.Collections.Generic.KeyValuePair<string, string[]> h in entry.ResponseHeaders)
            {
                // Defense in depth: the capture path filters the same headers
                // but entries written before the exclusion list existed (or by
                // a future option override during admin downgrade) must still
                // be filtered at replay time.
                if (opts.ExcludedResponseHeaders.Contains(h.Key))
                {
                    continue;
                }

                context.Response.Headers[h.Key] = h.Value;
            }
        }

        context.Response.Headers["Idempotent-Replayed"] = "true";

        if (entry.ResponseBody is { Length: > 0 })
        {
            await context.Response.Body.WriteAsync(entry.ResponseBody).ConfigureAwait(false);
        }
    }

    // =========================================================================
    // Payload hash (zero-allocation streaming SHA-256)
    // =========================================================================

    private async Task<string> ComputePayloadHashAsync(HttpContext context, string idempotencyKeyValue)
    {
        context.Request.EnableBuffering();

        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        // Incorporate: METHOD + routePattern + idempotencyKeyValue (key binding, not just body)
        AppendUtf8(sha256, context.Request.Method);
        AppendUtf8(sha256, "\n");
        AppendUtf8(sha256, context.GetEndpoint()?.DisplayName ?? context.Request.Path.Value ?? string.Empty);
        AppendUtf8(sha256, "\n");
        AppendUtf8(sha256, idempotencyKeyValue);
        AppendUtf8(sha256, "\n");

        // Stream the request body through the hash (capped at MaxBodySizeBytes)
        byte[] buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            int totalRead = 0;
            int read;
            while ((read = await context.Request.Body.ReadAsync(buffer, context.RequestAborted).ConfigureAwait(false)) > 0
                   && totalRead < _opts.MaxBodySizeBytes)
            {
                int toHash = Math.Min(read, _opts.MaxBodySizeBytes - totalRead);
                sha256.AppendData(buffer, 0, toHash);
                totalRead += toHash;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            context.Request.Body.Seek(0, SeekOrigin.Begin);
        }

        Span<byte> hashBytes = stackalloc byte[32];
        sha256.GetHashAndReset(hashBytes);
        return Convert.ToHexStringLower(hashBytes);
    }

    private static void AppendUtf8(IncrementalHash hash, string value)
    {
        int maxBytes = Encoding.UTF8.GetMaxByteCount(value.Length);
        byte[] rented = ArrayPool<byte>.Shared.Rent(maxBytes);
        try
        {
            int written = Encoding.UTF8.GetBytes(value, rented);
            hash.AppendData(rented, 0, written);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    // =========================================================================
    // Redis key builder
    // =========================================================================

    private string BuildRedisKey(HttpContext context, string idempotencyKeyValue)
    {
        string tenantSegment = _currentTenant.Id?.ToString() ?? "global";
        string userSegment = _currentUser.UserId ?? "anon";
        string method = context.Request.Method;
        string routePattern = context.GetEndpoint()?.DisplayName ?? context.Request.Path.Value ?? string.Empty;

        // Hash the client-supplied key to prevent Redis key injection
        string keyHash = ComputeSha256Hex(idempotencyKeyValue);

        return $"{_opts.KeyPrefix}:{tenantSegment}:{userSegment}:{method}:{routePattern}:{keyHash}";
    }

    private static string ComputeSha256Hex(string input)
    {
        int maxBytes = Encoding.UTF8.GetMaxByteCount(input.Length);
        byte[] rented = ArrayPool<byte>.Shared.Rent(maxBytes);
        try
        {
            int written = Encoding.UTF8.GetBytes(input, rented);
            Span<byte> hashBytes = stackalloc byte[32];
            SHA256.HashData(rented.AsSpan(0, written), hashBytes);
            return Convert.ToHexStringLower(hashBytes);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    // =========================================================================
    // Problem response helper (Utf8JsonWriter directly to response body — no MVC dependency)
    // =========================================================================

    // =========================================================================
    // Source-generated logger messages (CA1873 / CA1848 compliance)
    // =========================================================================

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Idempotency race: key {Key} disappeared between TryAcquire failure and re-read. Proceeding without lock.")]
    private static partial void LogRaceCondition(ILogger logger, string key);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Idempotency replay for key {Key} (status {Status}, completed {CompletedAt}).")]
    private static partial void LogReplay(ILogger logger, string key, int? status, DateTimeOffset? completedAt);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Idempotency execution timeout for key {Key} after {TimeoutSeconds}s. InProgress lock released.")]
    private static partial void LogExecutionTimeout(ILogger logger, string key, int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "HTTP 499: client disconnected for key {Key}. InProgress lock expires naturally in {TtlSeconds}s.")]
    private static partial void LogClientDisconnected(ILogger logger, string key, int ttlSeconds);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Idempotency response for key {Key} ({SizeBytes} bytes) exceeds MaxResponseSizeBytes ({MaxSizeBytes}). Storing tombstone; replays will return 413.")]
    private static partial void LogResponseTooLarge(ILogger logger, string key, long sizeBytes, int maxSizeBytes);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Idempotency replay for tombstoned key {Key} (reason: {Reason}). Request was executed once; response is not replayable.")]
    private static partial void LogTombstoneReplay(ILogger logger, string key, IdempotencyTombstoneReason? reason);

    // =========================================================================
    // Problem response helper (Utf8JsonWriter directly to response body — no MVC dependency)
    // =========================================================================

    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string title, string detail)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json; charset=utf-8";

        await using Utf8JsonWriter writer = new(context.Response.Body);
        writer.WriteStartObject();
        writer.WriteNumber("status"u8, statusCode);
        writer.WriteString("title"u8, title);
        writer.WriteString("detail"u8, detail);
        writer.WriteEndObject();
    }
}
