using System.Text.Json;
using System.Threading;
using Granit.Domain;
using Granit.Guids;
using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Diagnostics;
using Granit.Notifications.Domain;
using Granit.Notifications.EntityFrameworkCore.Extensions;
using Granit.Notifications.EntityFrameworkCore.Internal;
using Granit.Notifications.Extensions;
using Granit.Notifications.Handlers;
using Granit.Notifications.Messages;
using Granit.Timing;
using Granit.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

string dbPath = Path.Combine(AppContext.BaseDirectory, "issue947-verify.db");

try
{
    File.Delete(dbPath);
}
catch
{
    /* best effort */
}

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

RemoveNotificationDispatchWorker(builder.Services);

var sendCounter = new SendCounter();
builder.Services.AddSingleton(sendCounter);
builder.Services.AddSingleton<IClock, VerifierUtcClock>();
builder.Services.AddSingleton<ICurrentTenant, VerifierCurrentTenant>();

builder.AddGranitNotifications();
builder.Services.RemoveAll<INotificationChannel>();
builder.Services.AddScoped<CountingEmailChannel>();
builder.Services.AddScoped<INotificationChannel>(sp => sp.GetRequiredService<CountingEmailChannel>());
builder.Services.AddMetrics();

builder.AddGranitNotificationsEntityFrameworkCore(o => o.UseSqlite($"Data Source={dbPath}"));

using IHost host = builder.Build();

{
    await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
    IDbContextFactory<NotificationsDbContext> factory =
        scope.ServiceProvider.GetRequiredService<IDbContextFactory<NotificationsDbContext>>();
    await using NotificationsDbContext dbContext = await factory.CreateDbContextAsync().ConfigureAwait(false);
    await dbContext.Database.EnsureDeletedAsync().ConfigureAwait(false);
    await dbContext.Database.EnsureCreatedAsync().ConfigureAwait(false);
}

DeliverNotificationCommand command = new()
{
    DeliveryId = Guid.NewGuid(),
    NotificationId = Guid.NewGuid(),
    NotificationTypeName = "issue947.verify",
    Severity = NotificationSeverity.Info,
    RecipientUserId = "sample-user",
    ChannelName = NotificationChannels.Email,
    Data = JsonSerializer.SerializeToElement(new { note = "#947 verifier" }),
    OccurredAt = DateTimeOffset.UtcNow,
};

Console.WriteLine("Running two concurrent NotificationDeliveryHandler invocations (#947 verifier)...");

await using AsyncServiceScope scopeA = host.Services.CreateAsyncScope();
await using AsyncServiceScope scopeB = host.Services.CreateAsyncScope();

NotificationDeliveryHandler handlerA = CreateHandler(scopeA.ServiceProvider);
NotificationDeliveryHandler handlerB = CreateHandler(scopeB.ServiceProvider);

await Task.WhenAll(
    handlerA.HandleAsync(command, CancellationToken.None),
    handlerB.HandleAsync(command, CancellationToken.None)).ConfigureAwait(false);

Console.WriteLine($"Email sends observed (expected 1): {sendCounter.Total}");

Environment.Exit(sendCounter.Total == 1 ? 0 : 2);

static NotificationDeliveryHandler CreateHandler(IServiceProvider sp)
{
    IEnumerable<INotificationChannel> channels = sp.GetRequiredService<IEnumerable<INotificationChannel>>();
    INotificationDeliveryWriter writer = sp.GetRequiredService<INotificationDeliveryWriter>();

    return new NotificationDeliveryHandler(
        channels,
        writer,
        new SimpleGuidGenerator(),
        sp.GetRequiredService<IClock>(),
        NullLogger<NotificationDeliveryHandler>.Instance,
        sp.GetRequiredService<NotificationsMetrics>());
}

static void RemoveNotificationDispatchWorker(IServiceCollection services)
{
    foreach (ServiceDescriptor descriptor in services
                 .Where(d => d.ServiceType == typeof(IHostedService))
                 .Where(d => (d.ImplementationType?.FullName ?? string.Empty)
                     .Contains("NotificationDispatchWorker", StringComparison.Ordinal))
                 .ToArray())
    {
        services.Remove(descriptor);
    }
}

internal sealed class SendCounter
{
    private int _total;

    internal int Total => Volatile.Read(ref _total);

    internal void RecordSend() => _ = Interlocked.Increment(ref _total);
}

internal sealed class CountingEmailChannel(SendCounter counter) : INotificationChannel
{
    public string Name => NotificationChannels.Email;

    public Task SendAsync(NotificationDeliveryContext context, CancellationToken cancellationToken = default)
    {
        counter.RecordSend();
        return Task.CompletedTask;
    }
}

/// <summary>Minimal UTC stub — only <see cref="IClock.Now"/> is exercised by the delivery audit path.</summary>
internal sealed class VerifierUtcClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.UtcNow;

    public bool SupportsMultipleTimezone => false;

    public DateTimeOffset Normalize(DateTimeOffset dateTime) => dateTime.ToUniversalTime();

    public DateTimeOffset ConvertToUserTime(DateTimeOffset utcDateTime) => utcDateTime;

    public DateTimeOffset ConvertToUtc(DateTimeOffset dateTime) => dateTime.ToUniversalTime();
}

internal sealed class VerifierCurrentTenant : ICurrentTenant
{
    public bool IsAvailable => false;
    public Guid? Id => null;
    public string? Name => null;
    public IDisposable Change(Guid? id, string? name = null) => NoopDisposable.Instance;

    private sealed class NoopDisposable : IDisposable
    {
        internal static readonly NoopDisposable Instance = new();
        public void Dispose() { }
    }
}
