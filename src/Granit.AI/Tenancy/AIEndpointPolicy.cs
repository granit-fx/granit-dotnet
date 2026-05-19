using System.Collections.Frozen;

namespace Granit.AI.Tenancy;

/// <summary>
/// Per-provider policy enforced by <see cref="AIEndpointValidator"/> on every endpoint URL
/// configured by a tenant (workspace or setting) or by the host.
/// </summary>
/// <param name="RequireHttps">
/// When <c>true</c>, only <c>https</c> URLs are accepted. <c>false</c> only for Ollama
/// (local-loopback HTTP is the documented default).
/// </param>
/// <param name="AllowLoopback">
/// When <c>true</c>, <c>127.0.0.0/8</c>, <c>::1</c>, <c>::ffff:127.0.0.1</c> and the
/// <c>localhost</c> hostname are accepted. Set <c>true</c> for Ollama only.
/// </param>
/// <param name="AllowPrivateIp">
/// When <c>false</c> (the safe default for all hosted providers), the validator blocks
/// RFC 1918 ranges, CGNAT, link-local, ULA IPv6, multicast, and the well-known cloud
/// metadata IP literals and hostnames.
/// </param>
/// <param name="AllowedPorts">
/// Allow-list of TCP ports. Empty list means any port is accepted.
/// </param>
public sealed record AIEndpointPolicy(
    bool RequireHttps,
    bool AllowLoopback,
    bool AllowPrivateIp,
    FrozenSet<int>? AllowedPorts = null)
{
    /// <summary>Default policy for OpenAI / Anthropic (HTTPS only, no private / loopback).</summary>
    public static readonly AIEndpointPolicy HostedHttps = new(
        RequireHttps: true,
        AllowLoopback: false,
        AllowPrivateIp: false,
        AllowedPorts: new[] { 443 }.ToFrozenSet());

    /// <summary>Default policy for Azure OpenAI (HTTPS only).</summary>
    public static readonly AIEndpointPolicy AzureOpenAI = HostedHttps;

    /// <summary>Default policy for Ollama (loopback OK, private IP forbidden for tenants).</summary>
    public static readonly AIEndpointPolicy OllamaTenant = new(
        RequireHttps: false,
        AllowLoopback: true,
        AllowPrivateIp: false,
        AllowedPorts: new[] { 80, 443, 11434 }.ToFrozenSet());

    /// <summary>Permissive policy reserved for the Host scope (operator-configured options).</summary>
    /// <remarks>
    /// The host operator is trusted to point a non-tenant-configured endpoint anywhere — e.g.
    /// a private mesh address. Loopback + private IP are allowed but the validator still
    /// blocks the well-known cloud metadata endpoints (defence-in-depth).
    /// </remarks>
    public static readonly AIEndpointPolicy HostPermissive = new(
        RequireHttps: false,
        AllowLoopback: true,
        AllowPrivateIp: true,
        AllowedPorts: null);
}
