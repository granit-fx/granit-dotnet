using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates that all mutation endpoints (POST, PUT, DELETE, PATCH) in <c>*.Endpoints</c>
/// packages declare an <see cref="Granit.Http.Idempotency.Attributes.IdempotentAttribute"/>
/// via <c>.WithMetadata(new IdempotentAttribute(...))</c>, unless explicitly excluded.
/// </summary>
public sealed partial class IdempotencyConventionTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    /// <summary>
    /// Mutation endpoints that are intentionally excluded from idempotency protection.
    /// Each entry is the <c>WithName</c> operation ID of the endpoint.
    /// </summary>
    private static readonly HashSet<string> Exclusions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Authentication — not resource creation
        "AccountLogin",
        "AccountTwoFactorLogin",
        "BeginPasskeyAssertion",
        "CompletePasskeyAssertion",
        "ChallengeExternalLogin",

        // Intentionally repeatable — sends email / no stored side-effect
        "ForgotPassword",
        "ResendConfirmationEmail",

        // Idempotent by nature — session upsert / swap
        "SessionHeartbeat",
        "BackToImpersonator",

        // Inbound provider webhook — deduplicated by provider event ID
        "HandlePaymentWebhook",

        // Admin maintenance — safe to retry
        "CleanupOrphanedBlobs",

        // Query-like POST — generates a pre-signed URL
        "GenerateBlobDownloadUrl",

        // Diagnostic — no stored side-effect
        "TestWebhookPing",

        // Webhook redelivery — retry semantics, not creation
        "RedeliverWebhookEvent",
    };

    /// <summary>
    /// Endpoint modules that have been onboarded to idempotency. Only these modules
    /// are scanned. When a new module is onboarded, add it here and the test will
    /// enforce <c>IdempotentAttribute</c> on all its mutation endpoints.
    /// </summary>
    /// <remarks>
    /// Modules NOT in this set are not yet onboarded — their endpoints are not checked.
    /// This is an opt-in approach to allow incremental rollout.
    /// </remarks>
    private static readonly HashSet<string> OnboardedModules = new(StringComparer.OrdinalIgnoreCase)
    {
        "Granit.Payments.Endpoints",
        "Granit.Subscriptions.Endpoints",
        "Granit.Invoicing.Endpoints",
        "Granit.BlobStorage.Endpoints",
        "Granit.Webhooks.Endpoints",
        "Granit.Metering.Endpoints",
        "Granit.Identity.Local.Endpoints",
        "Granit.OpenIddict.Endpoints",
        "Granit.Authentication.ApiKeys.Endpoints",
    };

    /// <summary>
    /// Files that contain only GET endpoints, non-endpoint infrastructure, or
    /// OIDC protocol passthrough (Results.SignIn/Challenge) and should be skipped entirely.
    /// </summary>
    private static readonly HashSet<string> ExcludedFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        // OIDC passthrough — protocol-level flows
        "ConnectAuthorizationEndpoints",
        "ConnectTokenEndpoints",
        "ConnectLogoutEndpoints",
        "ConnectUserInfoEndpoints",

        // Infrastructure — not user-facing
        "LocalizationEndpointRouteBuilderExtensions",
        "QueryEndpointHandler",
    };

    /// <summary>
    /// Every POST, PUT, DELETE, and PATCH endpoint registration in <c>*.Endpoints</c>
    /// packages must include <c>.WithMetadata(new IdempotentAttribute</c> in its fluent chain,
    /// unless the endpoint is in the exclusion set.
    /// </summary>
    [Fact]
    public void Mutation_endpoints_should_declare_idempotency_metadata()
    {
        string srcDir = Path.Join(RepoRoot, "src");

        List<string> violations = [];

        foreach (string csFile in GetEndpointPackageSourceFiles(srcDir))
        {
            string content = File.ReadAllText(csFile);
            string fileName = Path.GetFileNameWithoutExtension(csFile);

            if (ExcludedFiles.Contains(fileName))
            {
                continue;
            }

            foreach (Match match in MutationEndpointRegistration().Matches(content))
            {
                string chain = ExtractFluentChain(content, match.Index);

                // Extract the WithName value to check against exclusions
                Match nameMatch = WithNameExtractor().Match(chain);
                if (nameMatch.Success && Exclusions.Contains(nameMatch.Groups[1].Value))
                {
                    continue;
                }

                // Endpoints excluded from OpenAPI description don't need metadata
                if (chain.Contains(".ExcludeFromDescription(", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!chain.Contains("IdempotentAttribute", StringComparison.Ordinal))
                {
                    string relativePath = Path.GetRelativePath(RepoRoot, csFile);
                    int lineNumber = content[..match.Index].Count(c => c == '\n') + 1;
                    string verb = match.Groups[1].Value;
                    string name = nameMatch.Success ? nameMatch.Groups[1].Value : "?";
                    violations.Add($"{relativePath}:{lineNumber} (.Map{verb} \"{name}\")");
                }
            }
        }

        violations.ShouldBeEmpty(
            "All mutation endpoints (POST/PUT/DELETE/PATCH) in *.Endpoints packages must declare " +
            "IdempotentAttribute via .WithMetadata(new IdempotentAttribute(...)). " +
            "If an endpoint should be excluded, add its WithName to the Exclusions set " +
            "in IdempotencyConventionTests with a rationale comment. " +
            $"Violators: {string.Join("; ", violations)}");
    }

    /// <summary>
    /// Extracts the fluent method chain starting at <paramref name="startIndex"/>
    /// until the terminating semicolon, tracking brace/parenthesis depth to skip
    /// nested lambdas.
    /// </summary>
    private static string ExtractFluentChain(string content, int startIndex)
    {
        int depth = 0;
        for (int i = startIndex; i < content.Length; i++)
        {
            char c = content[i];
            switch (c)
            {
                case '(' or '{':
                    depth++;
                    break;
                case ')' or '}':
                    depth--;
                    break;
                case ';' when depth <= 0:
                    return content[startIndex..i];
            }
        }

        return content[startIndex..];
    }

    /// <summary>
    /// Enumerates C# source files in onboarded <c>Granit.*.Endpoints</c> packages only.
    /// </summary>
    private static IEnumerable<string> GetEndpointPackageSourceFiles(string srcDir)
    {
        foreach (string csFile in Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            if (csFile.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                || csFile.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            string relativePath = Path.GetRelativePath(srcDir, csFile);
            string moduleName = relativePath.Split(Path.DirectorySeparatorChar)[0];

            if (!OnboardedModules.Contains(moduleName))
            {
                continue;
            }

            yield return csFile;
        }
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(IdempotencyConventionTests).Assembly.Location);
        while (dir is not null)
        {
            string gitPath = Path.Join(dir, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Could not find repository root (.git directory)");
    }

    /// <summary>
    /// Matches mutation endpoint registration calls: <c>.MapPost(</c>, <c>.MapPut(</c>,
    /// <c>.MapDelete(</c>, <c>.MapPatch(</c>. Captures the HTTP verb (group 1).
    /// </summary>
    [GeneratedRegex(@"\.Map(Post|Put|Delete|Patch)\s*\(", RegexOptions.Multiline)]
    private static partial Regex MutationEndpointRegistration();

    /// <summary>
    /// Extracts the operation ID from <c>.WithName("...")</c> in a fluent chain.
    /// Captures the name string (group 1).
    /// </summary>
    [GeneratedRegex(@"\.WithName\(\s*""([^""]+)""\s*\)", RegexOptions.Multiline)]
    private static partial Regex WithNameExtractor();
}
