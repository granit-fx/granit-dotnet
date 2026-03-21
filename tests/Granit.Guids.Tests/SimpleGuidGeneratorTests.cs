// =============================================================================
// Tests - SimpleGuidGenerator
// =============================================================================
// Verifies that SimpleGuidGenerator:
//   - Generates non-empty GUIDs
//   - Generates unique GUIDs
//   - Provides a static instance
// =============================================================================

using Shouldly;
using Xunit;

namespace Granit.Guids.Tests;

public sealed class SimpleGuidGeneratorTests
{
    [Fact]
    public void Create_ReturnsNonEmptyGuid()
    {
        // Arrange
        var generator = new SimpleGuidGenerator();

        // Act
        Guid guid = generator.Create();

        // Assert
        guid.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Create_GeneratesUniqueGuids()
    {
        // Arrange
        var generator = new SimpleGuidGenerator();

        // Act
        Guid guid1 = generator.Create();
        Guid guid2 = generator.Create();

        // Assert
        guid1.ShouldNotBe(guid2);
    }

    [Fact]
    public void Instance_IsNotNull() => SimpleGuidGenerator.Instance.ShouldNotBeNull();

    [Fact]
    public void Instance_CreateReturnsNonEmptyGuid()
    {
        // Act
        Guid guid = SimpleGuidGenerator.Instance.Create();

        // Assert
        guid.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Instance_ReturnsSameInstance()
    {
        // Act
        SimpleGuidGenerator first = SimpleGuidGenerator.Instance;
        SimpleGuidGenerator second = SimpleGuidGenerator.Instance;

        // Assert
        first.ShouldBeSameAs(second);
    }

    [Fact]
    public void Instance_ImplementsIGuidGenerator() =>
        // Assert
        SimpleGuidGenerator.Instance.ShouldBeAssignableTo<IGuidGenerator>();

    [Fact]
    public void Create_Generates10000UniqueGuids()
    {
        // Arrange
        SimpleGuidGenerator generator = SimpleGuidGenerator.Instance;
        HashSet<Guid> guids = [];

        // Act
        for (int i = 0; i < 10_000; i++)
        {
            guids.Add(generator.Create());
        }

        // Assert
        guids.Count.ShouldBe(10_000);
    }
}
