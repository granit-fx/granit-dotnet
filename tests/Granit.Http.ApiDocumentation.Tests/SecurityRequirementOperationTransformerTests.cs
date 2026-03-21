using Granit.Http.ApiDocumentation.Transformers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiDocumentation.Tests;

public sealed class SecurityRequirementOperationTransformerTests
{
    private readonly SecurityRequirementOperationTransformer _sut = new();

    [Fact]
    public async Task TransformAsync_AnonymousEndpoint_SetsSecurity()
    {
        var operation = new OpenApiOperation();
        OpenApiOperationTransformerContext context = BuildContext(new AllowAnonymousAttribute());

        await _sut.TransformAsync(operation, context, TestContext.Current.CancellationToken);

        operation.Security.ShouldNotBeNull();
        operation.Security.Count.ShouldBe(1);
    }

    [Fact]
    public async Task TransformAsync_ProtectedEndpoint_NullsSecurity()
    {
        var operation = new OpenApiOperation();
        OpenApiOperationTransformerContext context = BuildContext();

        await _sut.TransformAsync(operation, context, TestContext.Current.CancellationToken);

        operation.Security.ShouldBeNull();
    }

    private static OpenApiOperationTransformerContext BuildContext(params object[] metadata)
    {
        ActionDescriptor actionDescriptor = new()
        {
            EndpointMetadata = metadata.ToList(),
        };

        return new OpenApiOperationTransformerContext
        {
            DocumentName = "v1",
            Description = new ApiDescription
            {
                ActionDescriptor = actionDescriptor,
                RelativePath = "api/test",
            },
            ApplicationServices = Substitute.For<IServiceProvider>(),
        };
    }
}
