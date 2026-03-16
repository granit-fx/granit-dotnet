using System.Globalization;
using Granit.Security;
using Granit.Settings.Endpoints.Internal;
using Granit.Settings.Services;
using Granit.Timing;
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

            string? locale = await settingProvider
                .GetOrNullAsync(WellKnownSettingNames.PreferredCulture, context.RequestAborted)
                .ConfigureAwait(false);

            if (locale is not null && IsKnownCulture(locale))
            {
                var culture = CultureInfo.GetCultureInfo(locale);
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
            }

            string? timezone = await settingProvider
                .GetOrNullAsync(WellKnownSettingNames.PreferredTimezone, context.RequestAborted)
                .ConfigureAwait(false);

            if (timezone is not null)
            {
                ICurrentTimezoneProvider? timezoneProvider =
                    context.RequestServices.GetService<ICurrentTimezoneProvider>();

                if (timezoneProvider is not null)
                {
                    timezoneProvider.Timezone = timezone;
                }
            }
        }

        await _next(context).ConfigureAwait(false);
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
