using Shouldly;

namespace Granit.Localization.AI.Tests;

public sealed class TranslationContextTests
{
    [Fact]
    public void UiLabel_HasExpectedValue()
    {
        const TranslationContext context = TranslationContext.UiLabel;
        context.ShouldBe(TranslationContext.UiLabel);
    }

    [Fact]
    public void ErrorMessage_HasExpectedValue()
    {
        const TranslationContext context = TranslationContext.ErrorMessage;
        context.ShouldBe(TranslationContext.ErrorMessage);
    }

    [Fact]
    public void Notification_HasExpectedValue()
    {
        const TranslationContext context = TranslationContext.Notification;
        context.ShouldBe(TranslationContext.Notification);
    }

    [Fact]
    public void Description_HasExpectedValue()
    {
        const TranslationContext context = TranslationContext.Description;
        context.ShouldBe(TranslationContext.Description);
    }

    [Fact]
    public void Placeholder_HasExpectedValue()
    {
        const TranslationContext context = TranslationContext.Placeholder;
        context.ShouldBe(TranslationContext.Placeholder);
    }

    [Fact]
    public void AllValues_AreDefined()
    {
        TranslationContext[] values = Enum.GetValues<TranslationContext>();
        values.Length.ShouldBe(5);
    }
}
