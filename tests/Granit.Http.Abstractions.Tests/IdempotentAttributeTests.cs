using Granit.Http.Idempotency.Abstractions;
using Granit.Http.Idempotency.Attributes;
using Shouldly;
using Xunit;

namespace Granit.Http.Abstractions.Tests;

/// <summary>
/// Validates <see cref="IdempotentAttribute"/> defaults and <see cref="IIdempotencyMetadata"/> contract.
/// </summary>
public sealed class IdempotentAttributeTests
{
    [Fact]
    public void Default_Required_is_true()
    {
        IdempotentAttribute attribute = new();

        attribute.Required.ShouldBeTrue();
    }

    [Fact]
    public void Default_CompletedTtlSeconds_is_minus_one()
    {
        IdempotentAttribute attribute = new();

        attribute.CompletedTtlSeconds.ShouldBe(-1);
    }

    [Fact]
    public void Implements_IIdempotencyMetadata()
    {
        IdempotentAttribute attribute = new();

        attribute.ShouldBeAssignableTo<IIdempotencyMetadata>();
    }
}
