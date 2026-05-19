using Granit.Authorization.Endpoints.Dtos;
using Granit.Authorization.Endpoints.Internal;
using Granit.Http.ApiDocumentation;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Endpoints.Tests;

public sealed class AuthorizationSchemaExampleProviderTests
{
    [Fact]
    public void ImplementsISchemaExampleProvider()
    {
        AuthorizationSchemaExampleProvider provider = new();

        provider.ShouldBeAssignableTo<ISchemaExampleProvider>();
    }

    [Fact]
    public void GetExamples_ContainsMyPermissionsResponse()
    {
        AuthorizationSchemaExampleProvider provider = new();

        System.Collections.Generic.IReadOnlyDictionary<Type, System.Text.Json.Nodes.JsonNode> examples =
            provider.GetExamples();

        examples.ShouldContainKey(typeof(MyPermissionsResponse));
    }

    [Fact]
    public void GetExamples_ContainsPermissionGroupResponse()
    {
        AuthorizationSchemaExampleProvider provider = new();

        System.Collections.Generic.IReadOnlyDictionary<Type, System.Text.Json.Nodes.JsonNode> examples =
            provider.GetExamples();

        examples.ShouldContainKey(typeof(PermissionGroupResponse));
    }

    [Fact]
    public void GetExamples_ContainsPermissionGrantResponse()
    {
        AuthorizationSchemaExampleProvider provider = new();

        System.Collections.Generic.IReadOnlyDictionary<Type, System.Text.Json.Nodes.JsonNode> examples =
            provider.GetExamples();

        examples.ShouldContainKey(typeof(PermissionGrantResponse));
    }

    [Fact]
    public void GetExamples_ReturnsThreeEntries()
    {
        AuthorizationSchemaExampleProvider provider = new();

        System.Collections.Generic.IReadOnlyDictionary<Type, System.Text.Json.Nodes.JsonNode> examples =
            provider.GetExamples();

        examples.Count.ShouldBe(3);
    }
}
