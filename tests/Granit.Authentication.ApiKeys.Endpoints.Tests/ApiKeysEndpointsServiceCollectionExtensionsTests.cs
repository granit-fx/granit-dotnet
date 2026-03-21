using Granit.Authentication.ApiKeys.Endpoints.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Endpoints.Tests;

public sealed class ApiKeysEndpointsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitApiKeysEndpoints_ThrowsOnNullServices() =>
        Should.Throw<ArgumentNullException>(
            () => ApiKeysEndpointsServiceCollectionExtensions.AddGranitApiKeysEndpoints(null!));

    [Fact]
    public void AddGranitApiKeysEndpoints_ReturnsSameServiceCollection()
    {
        ServiceCollection services = new();

        IServiceCollection returned = services.AddGranitApiKeysEndpoints();

        returned.ShouldBeSameAs(services);
    }
}
