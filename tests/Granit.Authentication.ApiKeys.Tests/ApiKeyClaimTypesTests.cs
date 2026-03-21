using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Tests;

public sealed class ApiKeyClaimTypesTests
{
    [Fact]
    public void Permission_HasExpectedValue() =>
        ApiKeyClaimTypes.Permission.ShouldBe("permission");

    [Fact]
    public void ActorKind_HasExpectedValue() =>
        ApiKeyClaimTypes.ActorKind.ShouldBe("actor_kind");

    [Fact]
    public void ApiKeyId_HasExpectedValue() =>
        ApiKeyClaimTypes.ApiKeyId.ShouldBe("api_key_id");

    [Fact]
    public void ApiKeyType_HasExpectedValue() =>
        ApiKeyClaimTypes.ApiKeyType.ShouldBe("api_key_type");

    [Fact]
    public void Environment_HasExpectedValue() =>
        ApiKeyClaimTypes.Environment.ShouldBe("api_key_env");

    [Fact]
    public void TenantId_HasExpectedValue() =>
        ApiKeyClaimTypes.TenantId.ShouldBe("tenant_id");
}
