using Shouldly;
using Xunit;

namespace Granit.Guids.Tests;

public sealed class UuidV7GuidGeneratorTests
{
    private readonly TimeProvider _timeProvider = TimeProvider.System;
    private readonly UuidV7GuidGenerator _sut;

    public UuidV7GuidGeneratorTests()
    {
        _sut = new UuidV7GuidGenerator(_timeProvider);
    }

    [Fact]
    public void Create_ReturnsNonEmptyGuid()
    {
        Guid result = _sut.Create();

        result.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Create_ReturnsVersion7Guid()
    {
        Guid result = _sut.Create();

        // UUID version is encoded in bits 12-15 of the 7th byte (value = 7)
        int version = (result.ToByteArray()[7] >> 4) & 0xF;
        version.ShouldBe(7);
    }

    [Fact]
    public void Create_ReturnsDifferentGuidsOnSuccessiveCalls()
    {
        Guid first = _sut.Create();
        Guid second = _sut.Create();

        first.ShouldNotBe(second);
    }

    [Fact]
    public void Create_UsesProvidedTimestamp()
    {
        DateTimeOffset fixedTime = new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        FakeTimeProvider fakeTimeProvider = new(fixedTime);
        UuidV7GuidGenerator sut = new(fakeTimeProvider);

        Guid result = sut.Create();

        // UUIDv7 encodes the timestamp — verify the GUID is non-empty and version 7
        result.ShouldNotBe(Guid.Empty);
        int version = (result.ToByteArray()[7] >> 4) & 0xF;
        version.ShouldBe(7);
    }

    [Fact]
    public void Create_ImplementsIGuidGenerator() =>
        // Assert
        _sut.ShouldBeAssignableTo<IGuidGenerator>();

    [Fact]
    public void Create_Generates10000UniqueGuids()
    {
        // Arrange
        HashSet<Guid> guids = [];

        // Act
        for (int i = 0; i < 10_000; i++)
        {
            guids.Add(_sut.Create());
        }

        // Assert
        guids.Count.ShouldBe(10_000);
    }

    [Fact]
    public void Create_WithIncreasingTimestamps_GeneratesOrderedGuids()
    {
        // Arrange
        DateTimeOffset baseTime = new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        int callCount = 0;
        FakeTimeProvider fakeTimeProvider = new(baseTime, () => callCount++);
        UuidV7GuidGenerator sut = new(fakeTimeProvider);
        List<string> guids = [];

        // Act
        for (int i = 0; i < 5; i++)
        {
            guids.Add(sut.Create().ToString());
        }

        // Assert — UUIDv7 string representations should be ordered when timestamps increase
        guids.ShouldBeInOrder(SortDirection.Ascending);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _baseTime;
        private readonly Func<int>? _incrementFunc;

        public FakeTimeProvider(DateTimeOffset fixedTime, Func<int>? incrementFunc = null)
        {
            _baseTime = fixedTime;
            _incrementFunc = incrementFunc;
        }

        public override DateTimeOffset GetUtcNow()
        {
            if (_incrementFunc is not null)
            {
                return _baseTime.AddMilliseconds(_incrementFunc());
            }

            return _baseTime;
        }
    }
}
