using Granit.Authentication.JwtBearer.BackChannelLogout;
using Shouldly;
using Xunit;

namespace Granit.Authentication.JwtBearer.Tests.BackChannelLogout;

public sealed class BackChannelLogoutResultTests
{
    [Fact]
    public void Success_WithSessionAndSubject_HasCorrectProperties()
    {
        BackChannelLogoutResult result = new(true, "sid-123", "sub-456", null);

        result.Success.ShouldBeTrue();
        result.SessionId.ShouldBe("sid-123");
        result.SubjectId.ShouldBe("sub-456");
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void Failure_WithError_HasCorrectProperties()
    {
        BackChannelLogoutResult result = new(false, null, null, "Token validation failed.");

        result.Success.ShouldBeFalse();
        result.SessionId.ShouldBeNull();
        result.SubjectId.ShouldBeNull();
        result.Error.ShouldBe("Token validation failed.");
    }

    [Fact]
    public void Success_WithSessionIdOnly_HasNullSubjectId()
    {
        BackChannelLogoutResult result = new(true, "sid-only", null, null);

        result.Success.ShouldBeTrue();
        result.SessionId.ShouldBe("sid-only");
        result.SubjectId.ShouldBeNull();
    }

    [Fact]
    public void Success_WithSubjectIdOnly_HasNullSessionId()
    {
        BackChannelLogoutResult result = new(true, null, "sub-only", null);

        result.Success.ShouldBeTrue();
        result.SessionId.ShouldBeNull();
        result.SubjectId.ShouldBe("sub-only");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        BackChannelLogoutResult a = new(true, "sid", "sub", null);
        BackChannelLogoutResult b = new(true, "sid", "sub", null);

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        BackChannelLogoutResult a = new(true, "sid-1", "sub", null);
        BackChannelLogoutResult b = new(true, "sid-2", "sub", null);

        a.ShouldNotBe(b);
    }
}
