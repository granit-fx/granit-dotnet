// =============================================================================
// Tests - SequentialGuidGenerator
// =============================================================================
// Verifies that SequentialGuidGenerator:
//   - Generates non-empty GUIDs
//   - Generates unique GUIDs (10,000 iterations)
//   - Generates sequential GUIDs (ascending string ordering)
//   - Respects the configured sequential type
// =============================================================================

using Granit.Guids.Options;
using Granit.Timing;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Guids.Tests;

public sealed class SequentialGuidGeneratorTests
{
    private static SequentialGuidGenerator CreateGenerator(
        SequentialGuidType? guidType = null,
        IClock? clock = null)
    {
        IOptions<GuidGeneratorOptions> options = Substitute.For<IOptions<GuidGeneratorOptions>>();
        options.Value.Returns(new GuidGeneratorOptions
        {
            DefaultSequentialGuidType = guidType
        });
        if (clock is null)
        {
            clock = Substitute.For<IClock>();
            clock.Now.Returns(_ => DateTimeOffset.UtcNow);
        }

        return new SequentialGuidGenerator(options, clock);
    }

    [Fact]
    public void Create_ReturnsNonEmptyGuid()
    {
        // Arrange
        SequentialGuidGenerator generator = CreateGenerator();

        // Act
        Guid guid = generator.Create();

        // Assert
        guid.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Create_GeneratesUniqueGuids()
    {
        // Arrange
        SequentialGuidGenerator generator = CreateGenerator();
        HashSet<Guid> guids = [];

        // Act
        for (int i = 0; i < 10_000; i++)
        {
            guids.Add(generator.Create());
        }

        // Assert
        guids.Count.ShouldBe(10_000, "all GUIDs must be unique");
    }

    [Fact]
    public void Create_SequentialAsString_GeneratesOrderedGuids()
    {
        // Arrange — deterministic clock to avoid flaky timing on CI
        DateTimeOffset baseTime = DateTimeOffset.UtcNow;
        int callCount = 0;
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(_ => baseTime.AddMilliseconds(callCount++));
        SequentialGuidGenerator generator = CreateGenerator(SequentialGuidType.SequentialAsString, clock);
        List<string> guids = [];

        // Act
        for (int i = 0; i < 5; i++)
        {
            guids.Add(generator.Create().ToString());
        }

        // Assert — string representations should be in ascending order
        guids.ShouldBeInOrder(SortDirection.Ascending);
    }

    [Fact]
    public void Create_SequentialAtEnd_GeneratesNonEmptyGuids()
    {
        // Arrange
        SequentialGuidGenerator generator = CreateGenerator(SequentialGuidType.SequentialAtEnd);

        // Act
        Guid guid = generator.Create();

        // Assert
        guid.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Create_SequentialAsBinary_GeneratesNonEmptyGuids()
    {
        // Arrange
        SequentialGuidGenerator generator = CreateGenerator(SequentialGuidType.SequentialAsBinary);

        // Act
        Guid guid = generator.Create();

        // Assert
        guid.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Create_SequentialAsBinary_GeneratesUniqueGuids()
    {
        // Arrange
        SequentialGuidGenerator generator = CreateGenerator(SequentialGuidType.SequentialAsBinary);
        HashSet<Guid> guids = [];

        // Act
        for (int i = 0; i < 1_000; i++)
        {
            guids.Add(generator.Create(SequentialGuidType.SequentialAsBinary));
        }

        // Assert
        guids.Count.ShouldBe(1_000);
    }

    [Fact]
    public void Create_SequentialAtEnd_GeneratesUniqueGuids()
    {
        // Arrange
        SequentialGuidGenerator generator = CreateGenerator(SequentialGuidType.SequentialAtEnd);
        HashSet<Guid> guids = [];

        // Act
        for (int i = 0; i < 1_000; i++)
        {
            guids.Add(generator.Create(SequentialGuidType.SequentialAtEnd));
        }

        // Assert
        guids.Count.ShouldBe(1_000);
    }

    [Fact]
    public void Create_SequentialAsString_GeneratesUniqueGuids()
    {
        // Arrange
        SequentialGuidGenerator generator = CreateGenerator(SequentialGuidType.SequentialAsString);
        HashSet<Guid> guids = [];

        // Act
        for (int i = 0; i < 1_000; i++)
        {
            guids.Add(generator.Create(SequentialGuidType.SequentialAsString));
        }

        // Assert
        guids.Count.ShouldBe(1_000);
    }

    [Theory]
    [InlineData(SequentialGuidType.SequentialAsString)]
    [InlineData(SequentialGuidType.SequentialAsBinary)]
    [InlineData(SequentialGuidType.SequentialAtEnd)]
    public void Create_ExplicitType_ReturnsNonEmptyGuid(SequentialGuidType guidType)
    {
        // Arrange
        SequentialGuidGenerator generator = CreateGenerator(guidType);

        // Act
        Guid guid = generator.Create(guidType);

        // Assert
        guid.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Create_SequentialAsBinary_FirstSixBytesAreOrdered()
    {
        // Arrange — deterministic clock to guarantee ordering
        DateTimeOffset baseTime = DateTimeOffset.UtcNow;
        int callCount = 0;
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(_ => baseTime.AddMilliseconds(callCount++));
        SequentialGuidGenerator generator = CreateGenerator(SequentialGuidType.SequentialAsBinary, clock);

        // Act
        List<byte[]> firstSixBytesList = [];
        for (int i = 0; i < 5; i++)
        {
            byte[] bytes = generator.Create(SequentialGuidType.SequentialAsBinary).ToByteArray();
            firstSixBytesList.Add(bytes[..6]);
        }

        // Assert — first 6 bytes (timestamp) should be non-decreasing
        for (int i = 1; i < firstSixBytesList.Count; i++)
        {
            int comparison = CompareBytes(firstSixBytesList[i], firstSixBytesList[i - 1]);
            comparison.ShouldBeGreaterThanOrEqualTo(0,
                "first 6 bytes (timestamp) should be non-decreasing for SequentialAsBinary");
        }
    }

    [Fact]
    public void Create_SequentialAtEnd_LastSixBytesAreOrdered()
    {
        // Arrange — deterministic clock to guarantee ordering
        DateTimeOffset baseTime = DateTimeOffset.UtcNow;
        int callCount = 0;
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(_ => baseTime.AddMilliseconds(callCount++));
        SequentialGuidGenerator generator = CreateGenerator(SequentialGuidType.SequentialAtEnd, clock);

        // Act
        List<byte[]> lastSixBytesList = [];
        for (int i = 0; i < 5; i++)
        {
            byte[] bytes = generator.Create(SequentialGuidType.SequentialAtEnd).ToByteArray();
            lastSixBytesList.Add(bytes[10..16]);
        }

        // Assert — last 6 bytes (timestamp) should be non-decreasing
        for (int i = 1; i < lastSixBytesList.Count; i++)
        {
            int comparison = CompareBytes(lastSixBytesList[i], lastSixBytesList[i - 1]);
            comparison.ShouldBeGreaterThanOrEqualTo(0,
                "last 6 bytes (timestamp) should be non-decreasing for SequentialAtEnd");
        }
    }

    [Fact]
    public void Create_DefaultsToSequentialAsString()
    {
        // Arrange — deterministic clock, no type specified (defaults to SequentialAsString)
        DateTimeOffset baseTime = DateTimeOffset.UtcNow;
        int callCount = 0;
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(_ => baseTime.AddMilliseconds(callCount++));
        SequentialGuidGenerator generator = CreateGenerator(clock: clock);
        List<string> guids = [];

        // Act
        for (int i = 0; i < 5; i++)
        {
            guids.Add(generator.Create().ToString());
        }

        // Assert — if default is SequentialAsString, strings should be ordered
        guids.ShouldBeInOrder(SortDirection.Ascending);
    }

    [Fact]
    public void Create_WithExplicitType_UsesSpecifiedType()
    {
        // Arrange
        SequentialGuidGenerator generator = CreateGenerator(SequentialGuidType.SequentialAtEnd);
        List<Guid> guids = [];

        // Act
        for (int i = 0; i < 100; i++)
        {
            guids.Add(generator.Create());
        }

        // Assert — last 6 bytes should be non-decreasing (timestamp at end)
        List<byte[]> lastSixBytesList = guids
            .ConvertAll(g => g.ToByteArray()[10..16]);

        for (int i = 1; i < lastSixBytesList.Count; i++)
        {
            int comparison = CompareBytes(lastSixBytesList[i], lastSixBytesList[i - 1]);
            comparison.ShouldBeGreaterThanOrEqualTo(0,
                "last 6 bytes (timestamp) should be non-decreasing for SequentialAtEnd");
        }
    }

    private static int CompareBytes(byte[] a, byte[] b)
    {
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                return a[i].CompareTo(b[i]);
            }
        }

        return 0;
    }
}
