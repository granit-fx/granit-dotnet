using Microsoft.Extensions.Localization;
using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;

namespace Granit.Templating.Scriban.Internal;

/// <summary>
/// Scriban custom function that resolves localized strings via <see cref="IStringLocalizerFactory"/>.
/// Registered as <c>t</c> in the template context.
/// </summary>
/// <remarks>
/// <para>
/// Usage in templates:
/// <list type="bullet">
///   <item><c>{{ t "NotificationsEmail:ManagePreferences" }}</c></item>
///   <item><c>{{ t "NotificationsEmail:SubscribedReason" model.notification_group }}</c></item>
/// </list>
/// </para>
/// <para>
/// The key format is <c>{ResourceName}:{Key}</c>. The function resolves the localizer
/// for the resource name and looks up the key. Format arguments are passed positionally.
/// Returns the key itself if the resource or key is not found (Granit localizer convention).
/// </para>
/// </remarks>
internal sealed class TemplateLocalizationFunction(IStringLocalizerFactory localizerFactory) : IScriptCustomFunction
{
    public object? Invoke(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        if (arguments.Count == 0)
        {
            return "";
        }

        string? key = context.ObjectToString(arguments[0]);
        if (string.IsNullOrEmpty(key))
        {
            return "";
        }

        IStringLocalizer localizer = localizerFactory.Create(key, string.Empty);

        if (arguments.Count == 1)
        {
            return localizer[key].Value;
        }

        // Collect format arguments (positional after the key)
        object[] args = new object[arguments.Count - 1];
        for (int i = 1; i < arguments.Count; i++)
        {
            args[i - 1] = arguments[i] ?? "";
        }

        return localizer[key, args].Value;
    }

    public int RequiredParameterCount => 1;

    public int ParameterCount => 1;

    public ScriptVarParamKind VarParamKind => ScriptVarParamKind.Direct;

    public Type ReturnType => typeof(string);

    public ScriptParameterInfo GetParameterInfo(int index)
    {
        if (index == 0)
        {
            return new ScriptParameterInfo(typeof(string), "key");
        }

        return new ScriptParameterInfo(typeof(object), "arg");
    }

    public ValueTask<object?> InvokeAsync(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement) =>
        new(Invoke(context, callerContext, arguments, blockStatement));

    public int GetParameterIndexByName(string name) =>
        throw new NotImplementedException();
}
