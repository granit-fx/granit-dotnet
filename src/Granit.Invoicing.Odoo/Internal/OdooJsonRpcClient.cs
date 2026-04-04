using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Granit.Invoicing.Odoo.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Invoicing.Odoo.Internal;

/// <summary>
/// Low-level Odoo JSON-RPC client. Handles authentication, session re-authentication,
/// and model operations. Resilience (retry, circuit breaker, timeout) is provided by
/// the <c>Granit.Http.Resilience</c> pipeline configured on the "Odoo" named HttpClient.
/// </summary>
internal sealed partial class OdooJsonRpcClient(
    IHttpClientFactory httpClientFactory,
    IOptions<OdooOptions> options,
    ILogger<OdooJsonRpcClient> logger)
{
    private int? _uid;

    /// <summary>Authenticates with Odoo and stores the user ID.</summary>
    public async Task<int> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        if (_uid.HasValue)
        {
            return _uid.Value;
        }

        OdooOptions config = options.Value;
        OdooRpcResponse<int>? response = await CallAsync<int>(new OdooRpcRequest("call", new
        {
            service = "common",
            method = "authenticate",
            args = new object[] { config.Database, config.Login, config.ApiKey, new { } },
        }), cancellationToken).ConfigureAwait(false);

        if (response?.Result is null or 0)
        {
            throw new InvalidOperationException("Odoo authentication failed.");
        }

        _uid = response.Result;
        Log.Authenticated(logger, _uid.Value, config.Database);
        return _uid.Value;
    }

    /// <summary>Creates a record in an Odoo model.</summary>
    public async Task<int> CreateAsync(
        string model, Dictionary<string, object?> values,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithReauthAsync(async uid =>
        {
            OdooOptions config = options.Value;
            OdooRpcResponse<int>? response = await CallAsync<int>(new OdooRpcRequest("call", new
            {
                service = "object",
                method = "execute_kw",
                args = new object[] { config.Database, uid, config.ApiKey, model, "create", new[] { values } },
            }), cancellationToken).ConfigureAwait(false);

            return response?.Result ?? throw new InvalidOperationException($"Odoo create failed for model {model}.");
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads fields from an Odoo record.</summary>
    public async Task<JsonElement?> ReadAsync(
        string model, int id, string[] fields,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithReauthAsync(async uid =>
        {
            OdooOptions config = options.Value;
            OdooRpcResponse<JsonElement>? response = await CallAsync<JsonElement>(new OdooRpcRequest("call", new
            {
                service = "object",
                method = "execute_kw",
                args = new object[]
                {
                    config.Database, uid, config.ApiKey, model, "read",
                    new object[] { new[] { id } },
                    new { fields },
                },
            }), cancellationToken).ConfigureAwait(false);

            return response?.Result;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Updates fields on an existing Odoo record.</summary>
    public async Task UpdateAsync(
        string model, int id, Dictionary<string, object?> values,
        CancellationToken cancellationToken = default)
    {
        await ExecuteWithReauthAsync(async uid =>
        {
            OdooOptions config = options.Value;
            await CallAsync<bool>(new OdooRpcRequest("call", new
            {
                service = "object",
                method = "execute_kw",
                args = new object[] { config.Database, uid, config.ApiKey, model, "write", new object[] { new[] { id }, values } },
            }), cancellationToken).ConfigureAwait(false);

            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes an operation with automatic re-authentication on session expiry.
    /// If the first attempt fails with an Odoo session error, invalidates the cached UID,
    /// re-authenticates, and retries the operation exactly once.
    /// </summary>
    private async Task<T> ExecuteWithReauthAsync<T>(
        Func<int, Task<T>> operation, CancellationToken cancellationToken)
    {
        int uid = await AuthenticateAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return await operation(uid).ConfigureAwait(false);
        }
        catch (OdooSessionExpiredException)
        {
            Log.SessionExpired(logger);
            _uid = null;
            uid = await AuthenticateAsync(cancellationToken).ConfigureAwait(false);
            return await operation(uid).ConfigureAwait(false);
        }
    }

    private async Task<OdooRpcResponse<T>?> CallAsync<T>(
        OdooRpcRequest request, CancellationToken cancellationToken)
    {
        HttpClient client = httpClientFactory.CreateClient("Odoo");
        HttpResponseMessage httpResponse = await client
            .PostAsJsonAsync($"{options.Value.BaseUrl}/jsonrpc", request, cancellationToken)
            .ConfigureAwait(false);

        httpResponse.EnsureSuccessStatusCode();

        OdooRpcResponse<T>? response = await httpResponse.Content
            .ReadFromJsonAsync<OdooRpcResponse<T>>(cancellationToken)
            .ConfigureAwait(false);

        // Check for Odoo-level session/access errors in the JSON-RPC response
        if (response?.Error is { ValueKind: JsonValueKind.Object } error)
        {
            string? errorMessage = error.TryGetProperty("message", out JsonElement msg)
                ? msg.GetString()
                : null;

            if (IsSessionError(errorMessage))
            {
                throw new OdooSessionExpiredException(errorMessage);
            }

            Log.RpcError(logger, errorMessage);
            throw new InvalidOperationException($"Odoo RPC error: {errorMessage}");
        }

        return response;
    }

    private static bool IsSessionError(string? message) =>
        message is not null && (
            message.Contains("Session expired", StringComparison.OrdinalIgnoreCase)
            || message.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase)
            || message.Contains("session_expired", StringComparison.OrdinalIgnoreCase));

    private sealed record OdooRpcRequest(
        [property: JsonPropertyName("method")] string Method,
        [property: JsonPropertyName("params")] object Params)
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; } = "2.0";

        [JsonPropertyName("id")]
        public int Id { get; } = 1;
    }

    private sealed record OdooRpcResponse<T>(
        [property: JsonPropertyName("result")] T? Result,
        [property: JsonPropertyName("error")] JsonElement? Error);

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Odoo authenticated: uid={Uid}, db={Database}")]
        public static partial void Authenticated(ILogger logger, int uid, string database);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Odoo session expired, re-authenticating")]
        public static partial void SessionExpired(ILogger logger);

        [LoggerMessage(Level = LogLevel.Error, Message = "Odoo RPC error: {ErrorMessage}")]
        public static partial void RpcError(ILogger logger, string? errorMessage);
    }
}

/// <summary>Thrown when Odoo reports a session expiry or access denied error.</summary>
internal sealed class OdooSessionExpiredException(string? message)
    : InvalidOperationException(message ?? "Odoo session expired.");
