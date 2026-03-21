using Granit.Templating.Keys;
using Shouldly;
using Xunit;

namespace Granit.Templating.Tests.Keys;

public sealed class TextTemplateTypeTests
{
    private sealed record EmailData(string Recipient);

    private sealed class WelcomeEmailTemplateType : TextTemplateType<EmailData>
    {
        public override string Name => "Acme.WelcomeEmail";
    }

    [Fact]
    public void TextTemplateType_IsSubclassOfTemplateType()
    {
        WelcomeEmailTemplateType sut = new();

        sut.ShouldBeAssignableTo<TemplateType<EmailData>>();
    }

    [Fact]
    public void Name_ReturnsConcreteValue()
    {
        WelcomeEmailTemplateType sut = new();

        sut.Name.ShouldBe("Acme.WelcomeEmail");
    }

    [Fact]
    public void DataType_ReturnsEmailDataType()
    {
        WelcomeEmailTemplateType sut = new();

        sut.DataType.ShouldBe(typeof(EmailData));
    }

    [Fact]
    public void ResourceAssembly_ReturnsTestAssembly()
    {
        WelcomeEmailTemplateType sut = new();

        sut.ResourceAssembly.ShouldBe(typeof(TextTemplateTypeTests).Assembly);
    }
}
