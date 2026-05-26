using Shouldly;
using Xunit;

namespace Granit.Privacy.Endpoints.Tests;

public sealed class PrivacyExportRateLimitPoliciesTests
{
    [Fact]
    public void ExportCreate_PolicyNameIsStable()
    {
        // The policy name is part of the package's public contract — hosts wire
        // their quota under `RateLimiting:Policies:privacy-export-create` in
        // appsettings.json. Renaming this constant is a breaking change.
        PrivacyExportRateLimitPolicies.ExportCreate.ShouldBe("privacy-export-create");
    }
}
