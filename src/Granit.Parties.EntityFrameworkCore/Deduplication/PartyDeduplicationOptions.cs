namespace Granit.Parties.EntityFrameworkCore.Deduplication;

/// <summary>
/// Tunable thresholds and toggles for the Tier-2 (pg_trgm blocking) and Tier-3 (fuzzy
/// scoring) duplicate-detection pipelines that consume the canonical projections from
/// story #1297. Threshold values fall in the <c>[0.0, 1.0]</c> range — <c>1.0</c> means
/// "string equality", <c>0.0</c> means "anything goes".
/// </summary>
/// <remarks>
/// <para>
/// Defaults come from the prior-art research recorded in Epic
/// <see href="https://github.com/granit-fx/granit-dotnet/issues/1278">#1278</see>
/// (Salesforce / HubSpot / Dynamics 365 baselines): <c>0.7</c> on personal names is
/// strict enough to filter most random matches while still catching common typos and
/// diacritic variants; <c>0.6</c> on company names is looser because company names
/// have more legitimate variants ("Acme", "Acme Inc.", "Acme International").
/// </para>
/// <para>
/// Per-tenant overrides land later — admins may want to tune sensitivity in noisy
/// CRM imports. For v1, the values are global per app instance.
/// </para>
/// </remarks>
public sealed class PartyDeduplicationOptions
{
    /// <summary>
    /// Configuration section path, conventionally bound from <c>appsettings.json</c>:
    /// <code>{ "Granit": { "Parties": { "Deduplication": { ... } } } }</code>
    /// </summary>
    public const string SectionName = "Granit:Parties:Deduplication";

    /// <summary>
    /// pg_trgm <c>similarity()</c> threshold above which two <c>Name</c> values count
    /// as Tier-2 candidates. Default <c>0.7</c>. Operates on <c>lower(name)</c> via the
    /// GIST trigram index installed by <c>PartiesPostgresMigrationExtensions</c>.
    /// </summary>
    public double NameSimilarityThreshold { get; set; } = 0.7;

    /// <summary>
    /// Same as <see cref="NameSimilarityThreshold"/> but applied when the surviving party
    /// has <c>Kind == PartyKind.Company</c>. Default <c>0.6</c> (looser): company names
    /// carry more legitimate variants ("Acme" vs "Acme Inc.") and the sliding gap from
    /// the personal-name default is intentional.
    /// </summary>
    public double CompanySimilarityThreshold { get; set; } = 0.6;
}
