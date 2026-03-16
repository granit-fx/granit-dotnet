namespace Granit.Localization.AI;

/// <summary>
/// Describes the context of the text being translated, helping the LLM produce
/// more accurate and appropriately styled translations.
/// </summary>
public enum TranslationContext
{
    /// <summary>
    /// Short UI text such as a button label, menu item, or column header.
    /// </summary>
    UiLabel,

    /// <summary>
    /// Validation or error message shown to the user.
    /// </summary>
    ErrorMessage,

    /// <summary>
    /// Push notification, email subject, or in-app notification text.
    /// </summary>
    Notification,

    /// <summary>
    /// Longer descriptive or explanatory text (tooltips, help text, paragraphs).
    /// </summary>
    Description,

    /// <summary>
    /// Input field placeholder text.
    /// </summary>
    Placeholder,
}
