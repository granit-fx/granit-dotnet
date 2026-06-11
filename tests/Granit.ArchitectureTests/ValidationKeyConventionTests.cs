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

    // Category ∈ { Builtin (FV native), Format (format/identifier validators), Hint
    // (pattern hints), Problem (422 problem-details), Rule (framework-generic domain
    // rules shared across modules, e.g. batch-size limits) }.
    [GeneratedRegex(@"^Validation:(Builtin|Format|Hint|Problem|Rule):[A-Z][A-Za-z0-9]*$")]
    private static partial Regex ConformingKey();

    /// <summary>
    /// Keys still on the legacy flat scheme, pending migration to the category
    /// convention (or relocation to their owning module). The migration is complete —
    /// the backlog is empty. A NEW key must conform or be added here with justification.
    /// </summary>
    private static readonly HashSet<string> PendingMigration = new(StringComparer.Ordinal);

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
