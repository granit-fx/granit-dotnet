namespace Granit.Parties.Identity.Options;

/// <summary>
/// Host-level configuration for the
/// <see cref="GranitPartiesIdentityModule"/> bridge handlers.
/// </summary>
/// <remarks>
/// Bound from the <c>"Granit:Parties:Identity"</c> configuration section.
/// Hosts that need a non-EUR default for new <see cref="Granit.Parties.Domain.Party"/>
/// rows materialised by <see cref="Handlers.EnsurePartyForUserHandler"/> override
/// <see cref="DefaultCurrency"/> here — same convention as
/// <c>IDefaultPartySeeder.SeedForTenantAsync(defaultCurrency: "EUR")</c>.
/// </remarks>
public sealed class GranitPartiesIdentityOptions
{
    /// <summary>The configuration section name (<c>"Granit:Parties:Identity"</c>).</summary>
    public const string SectionName = "Granit:Parties:Identity";

    /// <summary>
    /// ISO 4217 currency code applied to <see cref="Granit.Parties.Domain.Party"/>
    /// rows the bridge materialises on <c>UserCreatedEto</c>. Defaults to
    /// <c>"EUR"</c> — admin can override per-Party post-creation.
    /// </summary>
    public string DefaultCurrency { get; set; } = "EUR";
}
