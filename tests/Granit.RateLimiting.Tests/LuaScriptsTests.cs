using Granit.RateLimiting.Internal;
using Shouldly;
using Xunit;

namespace Granit.RateLimiting.Tests;

public sealed class LuaScriptsTests
{
    [Fact]
    public void SlidingWindow_IsNotNullOrEmpty() => LuaScripts.SlidingWindow.ShouldNotBeNullOrWhiteSpace();

    [Fact]
    public void SlidingWindow_ContainsZremrangebyscore() => LuaScripts.SlidingWindow.ShouldContain("ZREMRANGEBYSCORE");

    [Fact]
    public void SlidingWindow_ContainsZadd() => LuaScripts.SlidingWindow.ShouldContain("ZADD");

    [Fact]
    public void SlidingWindow_UsesServerTime() => LuaScripts.SlidingWindow.ShouldContain("redis.call('TIME')");

    [Fact]
    public void FixedWindow_IsNotNullOrEmpty() => LuaScripts.FixedWindow.ShouldNotBeNullOrWhiteSpace();

    [Fact]
    public void FixedWindow_ContainsIncr() => LuaScripts.FixedWindow.ShouldContain("INCR");

    [Fact]
    public void FixedWindow_ContainsPexpire() => LuaScripts.FixedWindow.ShouldContain("PEXPIRE");

    [Fact]
    public void TokenBucket_IsNotNullOrEmpty() => LuaScripts.TokenBucket.ShouldNotBeNullOrWhiteSpace();

    [Fact]
    public void TokenBucket_ContainsHmget() => LuaScripts.TokenBucket.ShouldContain("HMGET");

    [Fact]
    public void TokenBucket_ContainsHset() => LuaScripts.TokenBucket.ShouldContain("HSET");

    [Fact]
    public void TokenBucket_UsesServerTime() => LuaScripts.TokenBucket.ShouldContain("redis.call('TIME')");
}
