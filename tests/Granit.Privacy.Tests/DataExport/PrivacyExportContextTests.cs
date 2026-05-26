using Granit.Privacy.DataExport;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Tests.DataExport;

public sealed class PrivacyExportContextTests
{
    [Fact]
    public void IsSelfService_True_WhenCallerMatchesSubject()
    {
        var userId = Guid.NewGuid();
        PrivacyExportContext context = new(
            RequestId: Guid.NewGuid(),
            SubjectUserId: userId,
            CallerUserId: userId,
            TenantId: null,
            Regulation: "EU_GDPR");

        context.IsSelfService.ShouldBeTrue();
    }

    [Fact]
    public void IsSelfService_False_WhenCallerDiffersFromSubject()
    {
        // Today's framework rejects this construction at every call site by always
        // passing the authenticated user for both arguments. The property exists so
        // the future Privacy.Exports.OnBehalfOf path has an explicit flag to check,
        // and so the subject-substitution analyzer has something to key off.
        PrivacyExportContext context = new(
            RequestId: Guid.NewGuid(),
            SubjectUserId: Guid.NewGuid(),
            CallerUserId: Guid.NewGuid(),
            TenantId: null,
            Regulation: "EU_GDPR");

        context.IsSelfService.ShouldBeFalse();
    }
}
