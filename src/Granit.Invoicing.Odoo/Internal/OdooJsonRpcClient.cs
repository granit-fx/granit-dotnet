using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Granit.Invoicing.Odoo.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Invoicing.Odoo.Internal;

/// <summary>
/// Low-level Odoo JSON-RPC client. Handles authentication and model operations.
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
        int uid = await AuthenticateAsync(cancellationToken).ConfigureAwait(false);
        OdooOptions config = options.Value;

        OdooRpcResponse<int>? response = await CallAsync<int>(new OdooRpcRequest("call", new
        {
            service = "object",
            method = "execute_kw",
            args = new object[] { config.Database, uid, config.ApiKey, model, "create", new[] { values } },
        }), cancellationToken).ConfigureAwait(false);

        return response?.Result ?? throw new InvalidOperationException($"Odoo create failed for model {model}.");
    }

    /// <summary>Reads fields from an Odoo record.</summary>
    public async Task<JsonElement?> ReadAsync(
        string model, int id, string[] fields,
        CancellationToken cancellationToken = default)
    {
        int uid = await AuthenticateAsync(cancellationToken).ConfigureAwait(false);
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
    }

    private async Task<OdooRpcResponse<T>?> CallAsync<T>(
        OdooRpcRequest request, CancellationToken cancellationToken)
    {
        HttpClient client = httpClientFactory.CreateClient("Odoo");
        HttpResponseMessage httpResponse = await client
            .PostAsJsonAsync($"{options.Value.BaseUrl}/jsonrpc", request, cancellationToken)
            .ConfigureAwait(false);

        httpResponse.EnsureSuccessStatusCode();

        return await httpResponse.Content
            .ReadFromJsonAsync<OdooRpcResponse<T>>(cancellationToken)
            .ConfigureAwait(false);
    }

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
    }
}
