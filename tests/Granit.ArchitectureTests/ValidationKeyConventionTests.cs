using System.Text.Json;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Enforces the framework validation-key naming convention (ADR-066 / "Proposition 1"):
/// every key in the <c>Granit.Validation</c> resource under the <c>Validation:</c>
/// namespace MUST be <c>Validation:{Category}:{Rule}</c> where
/// <c>Category ∈ { Builtin, Format, Hint, Problem }</c> and <c>Rule</c> is PascalCase.
/// </summary>
/// <remarks>
/// <para>
/// Domain-specific validation messages do NOT belong in the framework resource at all —
/// they live in their owning module's resource as <c>{Module}:Validation:{Rule}</c>
/// (e.g. <c>Payments:Validation:AmountOutOfRange</c>). This test guards the framework
/// resource only; the per-module rule is enforced by each app/business repo.
/// </para>
/// <para>
/// <see cref="PendingMigration"/> is the canonical backlog of keys still on the legacy
/// flat scheme. The list shrinks to empty as the migration proceeds (built-ins →
/// <c>Builtin:</c>, format validators → <c>Format:</c>, hints → <c>Hint:</c>,
/// <c>ProblemTitle</c> → <c>Problem:Title</c>, and domain keys relocated to their
/// modules). A stale entry (key migrated or removed) fails the test, keeping the
/// backlog honest. A NEW key must either conform or be added here with justification.
/// </para>
/// </remarks>
public sealed partial class ValidationKeyConventionTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [GeneratedRegex(@"^Validation:(Builtin|Format|Hint|Problem):[A-Z][A-Za-z0-9]*$")]
    private static partial Regex ConformingKey();

    /// <summary>
    /// Keys still on the legacy flat scheme, pending migration to the category
    /// convention (or relocation to their owning module). MUST shrink to empty.
    /// </summary>
    private static readonly HashSet<string> PendingMigration = new(StringComparer.Ordinal)
    {
        // → Validation:Builtin:* (drop the redundant "Validator" suffix); remapped in GranitErrorCodeLanguageManager.
        "Validation:AsyncPredicateValidator",
        "Validation:CreditCardValidator",
        "Validation:EmailValidator",
        "Validation:EmptyValidator",
        "Validation:EqualValidator",
        "Validation:ExactLengthValidator",
        "Validation:ExclusiveBetweenValidator",
        "Validation:GreaterThanOrEqualValidator",
        "Validation:GreaterThanValidator",
        "Validation:InclusiveBetweenValidator",
        "Validation:LengthValidator",
        "Validation:LessThanOrEqualValidator",
        "Validation:LessThanValidator",
        "Validation:MaximumLengthValidator",
        "Validation:MinimumLengthValidator",
        "Validation:NotEmptyValidator",
        "Validation:NotEqualValidator",
        "Validation:NotNullValidator",
        "Validation:NullValidator",
        "Validation:PredicateValidator",
        "Validation:RegularExpressionValidator",
        "Validation:ScalePrecisionValidator",

        // → Validation:Format:* (the "Format" category already implies "invalid format").
        "Validation:InvalidAbsoluteUri",
        "Validation:InvalidBase64String",
        "Validation:InvalidBcp47LanguageTag",
        "Validation:InvalidBicSwift",
        "Validation:InvalidColorHex",
        "Validation:InvalidCreditCard",
        "Validation:InvalidE164Phone",
        "Validation:InvalidEmail",
        "Validation:InvalidGeoLatitude",
        "Validation:InvalidGeoLongitude",
        "Validation:InvalidIban",
        "Validation:InvalidIpv4Address",
        "Validation:InvalidIpv6Address",
        "Validation:InvalidIso3166Alpha2",
        "Validation:InvalidIso4217CurrencyCode",
        "Validation:InvalidIso8601Duration",
        "Validation:InvalidLei",
        "Validation:InvalidMacAddress",
        "Validation:InvalidSepaCreditorIdentifier",
        "Validation:InvalidSlug",
        "Validation:InvalidUrl",
        "Validation:InvalidUuid",
        "Validation:UrlMustBeHttps",

        // → Validation:Hint:* (singular).
        "Validation:Hints:Alpha2Code",
        "Validation:Hints:Alpha3Code",
        "Validation:Hints:NumericCode",

        // → Validation:Problem:Title (the 422 problem-details title).
        "Validation:ProblemTitle",

        // → relocate to the owning module's resource as {Module}:Validation:{Rule} (Phase 2).
        // These have no framework consumer; they were left behind when the owning modules
        // (Analytics, Metering, Payments, Invoicing, Entities, QueryEngine, …) moved to granit-business.
        "Validation:CalendarRangeInverted",
        "Validation:CalendarRangeTooWide",
        "Validation:CodeInvalidFormat",
        "Validation:EntityViewShareEmptyAudience",
        "Validation:MaxBatchSize",
        "Validation:MaxMetadataKeys",
        "Validation:MeteringDistinctPropertyNotAllowed",
        "Validation:MeteringDistinctPropertyRequired",
        "Validation:MeteringRecomputeWindowInvalid",
        "Validation:PaymentAmountOutOfRange",
        "Validation:PeriodSpecBoundsCode",
        "Validation:PeriodSpecOrderingCode",
        "Validation:RegroupGroupKeyRequired",
        "Validation:ReorderDeltaIllFormed",
        "Validation:ScopeRequired",
        "Validation:SearchTermRequired",
        "Validation:TargetIdRequired",
        "Validation:TargetTypeRequired",
        "Validation:TooManyDeltas",
        "Validation:ValidToAfterValidFrom",
    };

    [Fact]
    public void Framework_validation_keys_follow_the_category_convention()
    {
        IReadOnlyList<string> keys = LoadFrameworkValidationKeys();
        keys.ShouldNotBeEmpty();

        List<string> violations = [.. keys
            .Where(k => !ConformingKey().IsMatch(k) && !PendingMigration.Contains(k))
            .Order(StringComparer.Ordinal)];

        violations.ShouldBeEmpty(
            $"{violations.Count} validation key(s) violate the convention. Use "
            + "Validation:{Builtin|Format|Hint|Problem}:{PascalCase}, OR move domain-specific "
            + "messages to their owning module's resource as {Module}:Validation:{Rule}. "
            + "If a key is intentionally legacy, add it to PendingMigration with justification:"
            + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void PendingMigration_has_no_stale_entries()
    {
        HashSet<string> live = [.. LoadFrameworkValidationKeys()];

        List<string> stale = [.. PendingMigration
            .Where(k => !live.Contains(k))
            .Order(StringComparer.Ordinal)];

        stale.ShouldBeEmpty(
            $"{stale.Count} PendingMigration entr(y/ies) no longer exist in the Validation "
            + "resource — remove them as the migration completes:"
            + Environment.NewLine + string.Join(Environment.NewLine, stale));
    }

    private static IReadOnlyList<string> LoadFrameworkValidationKeys()
    {
        string dir = Path.Join(
            RepoRoot, "src", "Granit.Validation", "Localization", "Validation");

        // The default-culture file carries the full key set; regional files only differ.
        string enFile = Path.Join(dir, "en.json");
        File.Exists(enFile).ShouldBeTrue($"Expected framework validation resource at {enFile}.");

        using FileStream stream = File.OpenRead(enFile);
        using var document = JsonDocument.Parse(stream);

        JsonElement texts = document.RootElement.GetProperty("texts");
        return [.. texts.EnumerateObject()
            .Select(p => p.Name)
            .Where(name => name.StartsWith("Validation:", StringComparison.Ordinal))];
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(ValidationKeyConventionTests).Assembly.Location);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Join(dir, ".git")) || File.Exists(Path.Join(dir, ".git")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Could not locate the repository root (.git).");
    }
}
