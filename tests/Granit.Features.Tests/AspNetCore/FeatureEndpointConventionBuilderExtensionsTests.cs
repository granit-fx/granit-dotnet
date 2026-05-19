using Granit.Features.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Shouldly;
using Xunit;

namespace Granit.Features.Tests.AspNetCore;

public sealed class FeatureEndpointConventionBuilderExtensionsTests
{
    /// <summary>
    /// Test double that captures conventions added via <see cref="IEndpointConventionBuilder.Add"/>.
    /// </summary>
    private sealed class TrackingEndpointConventionBuilder : IEndpointConventionBuilder
    {
        public List<Action<EndpointBuilder>> Conventions { get; } = [];

        public void Add(Action<EndpointBuilder> convention) => Conventions.Add(convention);

        public void Finally(Action<EndpointBuilder> convention) { }
    }

    [Fact]
    public void RequiresFeature_ReturnsBuilder_ForChaining()
    {
        TrackingEndpointConventionBuilder builder = new();

        IEndpointConventionBuilder result = builder.RequiresFeature("App.VideoConsultation");

        result.ShouldNotBeNull();
    }

    [Fact]
    public void RequiresFeature_AddsConvention()
    {
        TrackingEndpointConventionBuilder builder = new();

        builder.RequiresFeature("App.VideoConsultation");

        builder.Conventions.ShouldNotBeEmpty("at least one convention should be added for the endpoint filter");
    }
}
