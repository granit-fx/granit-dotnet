using Granit.Notifications.Smtp.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Smtp.Tests;

public sealed class SmtpOptionsTests
{
    [Fact]
    public void SectionName_IsNotificationsSmtp() =>
        SmtpOptions.SectionName.ShouldBe("Notifications:Smtp");

    [Fact]
    public void Host_Default_IsLocalhost() =>
        new SmtpOptions().Host.ShouldBe("localhost");

    [Fact]
    public void Port_Default_Is587() =>
        new SmtpOptions().Port.ShouldBe(587);

    [Fact]
    public void UseSsl_Default_IsTrue() =>
        new SmtpOptions().UseSsl.ShouldBeTrue();

    [Fact]
    public void Username_Default_IsNull() =>
        new SmtpOptions().Username.ShouldBeNull();

    [Fact]
    public void Password_Default_IsNull() =>
        new SmtpOptions().Password.ShouldBeNull();
}
