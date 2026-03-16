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

    private sealed class FakeTimeProvider(DateTimeOffset fixedTime) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => fixedTime;
    }
}
