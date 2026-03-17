using Granit.ArchitectureTests.Abstractions.Rules;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates DTO naming conventions in endpoint packages:
/// no *Dto suffix, use *Request / *Response instead.
/// </summary>
public sealed class DtoConventionTests
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture = GranitArchitecture.Instance;

    [Fact]
    public void Endpoint_types_should_not_use_Dto_suffix() =>
        NamingConventionRules.EndpointTypesShouldNotUseDtoSuffix(Architecture,
            "Granit.Authentication.ApiKeys.Endpoints",
            "Granit.Authorization.Endpoints",
            "Granit.BackgroundJobs.Endpoints",
            "Granit.Http.Cookies.Endpoints",
            "Granit.DataExchange.Endpoints",
            "Granit.Identity.Endpoints",
            "Granit.Localization.Endpoints",
            "Granit.Notifications.Endpoints",
            "Granit.Querying.Endpoints",
            "Granit.ReferenceData.Endpoints",
            "Granit.Templating.Endpoints",
            "Granit.Timeline.Endpoints",
            "Granit.Workflow.Endpoints");
}
