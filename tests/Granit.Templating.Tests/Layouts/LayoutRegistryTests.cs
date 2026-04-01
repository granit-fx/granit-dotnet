using Granit.Templating.Layouts;
using Granit.Templating.Layouts.Internal;
using Shouldly;
using Xunit;

namespace Granit.Templating.Tests.Layouts;

public sealed class LayoutRegistryTests
{
    [Fact]
    public void GetLayoutName_ExactMatch_ReturnsLayout()
    {
        LayoutRegistry sut = new([new LayoutRegistration("Billing.Invoice", "Layout.Document")]);

        sut.GetLayoutName("Billing.Invoice").ShouldBe("Layout.Document");
    }

    [Fact]
    public void GetLayoutName_PrefixMatch_ReturnsLayout()
    {
        LayoutRegistry sut = new([new LayoutRegistration("Security.*", "Layout.Email")]);

        sut.GetLayoutName("Security.Welcome").ShouldBe("Layout.Email");
    }

    [Fact]
    public void GetLayoutName_NoMatch_ReturnsNull()
    {
        LayoutRegistry sut = new([new LayoutRegistration("Security.*", "Layout.Email")]);

        sut.GetLayoutName("Billing.Invoice").ShouldBeNull();
    }

    [Fact]
    public void GetLayoutName_ExactMatchTakesPrecedenceOverPrefix()
    {
        LayoutRegistry sut = new(
        [
            new LayoutRegistration("Billing.*", "Layout.Email"),
            new LayoutRegistration("Billing.Invoice", "Layout.Document"),
        ]);

        sut.GetLayoutName("Billing.Invoice").ShouldBe("Layout.Document");
        sut.GetLayoutName("Billing.Receipt").ShouldBe("Layout.Email");
    }

    [Fact]
    public void GetLayoutName_HigherPriorityPrefixWins()
    {
        LayoutRegistry sut = new(
        [
            new LayoutRegistration("Notifications.*", "Layout.Generic", Priority: 0),
            new LayoutRegistration("Notifications.*", "Layout.Email", Priority: 10),
        ]);

        sut.GetLayoutName("Notifications.Welcome").ShouldBe("Layout.Email");
    }

    [Fact]
    public void GetLayoutName_LongerPrefixWinsAtSamePriority()
    {
        LayoutRegistry sut = new(
        [
            new LayoutRegistration("Billing.*", "Layout.Generic"),
            new LayoutRegistration("Billing.Finance.*", "Layout.Finance"),
        ]);

        sut.GetLayoutName("Billing.Finance.Invoice").ShouldBe("Layout.Finance");
        sut.GetLayoutName("Billing.Receipt").ShouldBe("Layout.Generic");
    }

    [Fact]
    public void GetLayoutName_EmptyRegistry_ReturnsNull()
    {
        LayoutRegistry sut = new([]);

        sut.GetLayoutName("Anything").ShouldBeNull();
    }

    [Fact]
    public void GetAllLayoutNames_ReturnsDeduplicated()
    {
        LayoutRegistry sut = new(
        [
            new LayoutRegistration("Security.*", "Layout.Email"),
            new LayoutRegistration("Notifications.*", "Layout.Email"),
            new LayoutRegistration("Billing.*", "Layout.Document"),
        ]);

        IReadOnlyList<string> layouts = sut.GetAllLayoutNames();

        layouts.ShouldBe(["Layout.Document", "Layout.Email"]); // sorted
    }

    [Fact]
    public void GetAllLayoutNames_EmptyRegistry_ReturnsEmpty()
    {
        LayoutRegistry sut = new([]);

        sut.GetAllLayoutNames().ShouldBeEmpty();
    }
}
