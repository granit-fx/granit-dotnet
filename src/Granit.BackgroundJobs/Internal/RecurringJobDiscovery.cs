using System.Reflection;
using Cronos;
using Granit.BackgroundJobs.Domain;
using Granit.Reflection;

namespace Granit.BackgroundJobs.Internal;

/// <summary>
/// Scans assemblies for message types decorated with <see cref="RecurringJobAttribute"/>
/// and produces <see cref="RecurringJobRegistration"/> descriptors for seeding.
/// </summary>
internal static class RecurringJobDiscovery
{
    /// <summary>
    /// Returns all <see cref="RecurringJobRegistration"/> found in <paramref name="assemblies"/>.
    /// </summary>
    internal static IReadOnlyList<RecurringJobRegistration> Discover(IEnumerable<Assembly> assemblies)
    {
        List<RecurringJobRegistration> registrations = [];

        foreach (Assembly assembly in assemblies)
        {
            foreach (Type type in assembly.GetLoadableTypes())
            {
                RecurringJobAttribute? attr = type.GetCustomAttribute<RecurringJobAttribute>();
                if (attr is null)
                {
                    continue;
                }

                string? assemblyQualifiedName = type.AssemblyQualifiedName;
                if (assemblyQualifiedName is null)
                {
                    continue;
                }

                registrations.Add(new RecurringJobRegistration(
                    JobName: attr.Name,
                    CronExpression: attr.CronExpression,
                    MessageType: assemblyQualifiedName));
            }
        }

        return registrations;
    }

    /// <summary>
    /// Validates every registration's cron expression — failing fast at startup instead
    /// of silently never scheduling the job.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// When a registration carries a cron expression that Cronos cannot parse.
    /// </exception>
    internal static void ValidateCronExpressions(IEnumerable<RecurringJobRegistration> registrations)
    {
        foreach (RecurringJobRegistration registration in registrations)
        {
            try
            {
                CronSchedulerHelper.Parse(registration.CronExpression);
            }
            catch (CronFormatException ex)
            {
                throw new InvalidOperationException(
                    $"Recurring job '{registration.JobName}' ({registration.MessageType}) declares " +
                    $"an invalid cron expression '{registration.CronExpression}'.", ex);
            }
        }
    }
}
