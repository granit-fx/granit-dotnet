using System.Globalization;
using Granit.Settings.Endpoints.Internal;
using Granit.Settings.Services;
using Granit.Timing;
using Granit.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Settings.Endpoints.Middleware;

/// <summary>
/// Middleware that hydrates <see cref="CultureInfo.CurrentUICulture"/> and
/// <see cref="ICurrentTimezoneProvider"/> from the authenticated user's settings.
/// </summary>
/// <remarks>
/// Must run <strong>after</strong> authentication middleware and <strong>before</strong> endpoint handlers.
/// For anonymous requests, this middleware is a no-op.
/// </remarks>
public sealed class SettingsCultureMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        ICurrentUserService currentUser = context.RequestServices.GetRequiredService<ICurrentUserService>();

        if (currentUser.IsAuthenticated && currentUser.UserId is not null)
        {
            ISettingProvider settingProvider = context.RequestServices.GetRequiredService<ISettingProvider>();

            // The culture MUST be assigned in this method's async context, not inside a nested
            // async helper: CultureInfo.Current(UI)Culture is backed by an AsyncLocal, and a value
            // set inside a callee does not flow back to the caller. Setting it here — the same
            // async frame that awaits _next — is what makes it visible to downstream handlers.
            CultureInfo? culture = await ResolveCultureAsync(context, settingProvider).ConfigureAwait(false);
            if (culture is not null)
            {
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
            }

            await ApplyTimezoneAsync(context, settingProvider).ConfigureAwait(false);
            await ApplyFirstDayOfWeekAsync(context, settingProvider).ConfigureAwait(false);
        }

        await _next(context).ConfigureAwait(false);
    }

    private static async Task<CultureInfo?> ResolveCultureAsync(HttpContext context, ISettingProvider settingProvider)
    {
        string? locale = await settingProvider
            .GetOrNullAsync(WellKnownSettingNames.PreferredCulture, context.RequestAborted)
            .ConfigureAwait(false);

        return locale is not null && IsKnownCulture(locale)
            ? CultureInfo.GetCultureInfo(locale)
            : null;
    }

    private static async Task ApplyTimezoneAsync(HttpContext context, ISettingProvider settingProvider)
    {
        string? timezone = await settingProvider
            .GetOrNullAsync(WellKnownSettingNames.PreferredTimezone, context.RequestAborted)
            .ConfigureAwait(false);

        if (timezone is not null
            && context.RequestServices.GetService<ICurrentTimezoneProvider>() is { } timezoneProvider)
        {
            timezoneProvider.Timezone = timezone;
        }
    }

    private static async Task ApplyFirstDayOfWeekAsync(HttpContext context, ISettingProvider settingProvider)
    {
        string? firstDayOfWeek = await settingProvider
            .GetOrNullAsync(WellKnownSettingNames.PreferredFirstDayOfWeek, context.RequestAborted)
            .ConfigureAwait(false);

        if (firstDayOfWeek is not null
            && Enum.TryParse(firstDayOfWeek, ignoreCase: true, out DayOfWeek day)
            && context.RequestServices.GetService<ICurrentFirstDayOfWeekProvider>() is { } firstDayOfWeekProvider)
        {
            firstDayOfWeekProvider.FirstDayOfWeek = day;
        }
    }

    private static bool IsKnownCulture(string name)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(name);

            // .NET 8+ creates custom CultureInfo for unknown names instead of throwing.
            // A known culture has a non-empty LCID (0x1000 = unknown) or matches by name.
            return culture.LCID != 0x1000
                || CultureInfo.GetCultures(CultureTypes.AllCultures)
                    .Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }
}
