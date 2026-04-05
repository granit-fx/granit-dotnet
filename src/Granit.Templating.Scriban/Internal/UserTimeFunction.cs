using System.Globalization;
using Granit.Timing;
using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;

namespace Granit.Templating.Scriban.Internal;

/// <summary>
/// Scriban custom function <c>to_user_time</c> that converts a <see cref="DateTimeOffset"/>
/// from UTC to the current user's timezone via <see cref="IClock.ConvertToUserTime"/>.
/// </summary>
/// <remarks>
/// Usage in templates:
/// <code>{{ model.trial_ends_at | to_user_time }}</code>
/// <code>{{ model.trial_ends_at | to_user_time | date.to_string "%B %d, %Y at %H:%M" }}</code>
///
/// When no user timezone is set (via <see cref="ICurrentTimezoneProvider"/>),
/// the date is returned unchanged (UTC).
/// </remarks>
internal sealed class UserTimeFunction(IClock clock) : IScriptCustomFunction
{
    public object? Invoke(TemplateContext context, ScriptNode? callerContext,
        ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        if (arguments.Count == 0)
        {
            return string.Empty;
        }

        object? arg = arguments[0];

        return arg switch
        {
            DateTimeOffset dto => clock.ConvertToUserTime(dto),
            DateTime dt => clock.ConvertToUserTime(new DateTimeOffset(dt, TimeSpan.Zero)),
            string s when DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset parsed) =>
                clock.ConvertToUserTime(parsed),
            _ => arg ?? string.Empty,
        };
    }

    public int RequiredParameterCount => 1;

    public int ParameterCount => 1;

    public ScriptVarParamKind VarParamKind => ScriptVarParamKind.None;

    public Type ReturnType => typeof(DateTimeOffset);

    public ScriptParameterInfo GetParameterInfo(int index) =>
        new(typeof(object), "datetime");

    public ValueTask<object?> InvokeAsync(TemplateContext context, ScriptNode? callerContext,
        ScriptArray arguments, ScriptBlockStatement? blockStatement) =>
        new(Invoke(context, callerContext, arguments, blockStatement));
}
