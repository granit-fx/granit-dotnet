using System.Threading.Channels;
using Granit.AuditLog.Domain;
using Granit.AuditLog.EntityFrameworkCore.Internal.Services;
using Granit.AuditLog.Messages;
using Shouldly;
using Xunit;

namespace Granit.AuditLog.EntityFrameworkCore.Tests.Internal;

public sealed class ChannelAuditLogPublisherTests
{
    [Fact]
    public async Task PublishAsync_WritesBatchToChannel()
    {
        var channel = Channel.CreateUnbounded<AuditLogBatch>();
        ChannelAuditLogPublisher publisher = new(channel);

        AuditLogBatch batch = new(
            Timestamp: DateTimeOffset.UtcNow,
            UserId: "user-1",
            UserName: "Test",
            Category: AuditLogCategory.DataMutation,
            IpAddress: null,
            UserAgent: null,
            TenantId: null,
            CorrelationId: null,
            EntityChanges: []);

        await publisher.PublishAsync(batch, TestContext.Current.CancellationToken);

        bool read = channel.Reader.TryRead(out AuditLogBatch? received);
        read.ShouldBeTrue();
        received.ShouldBe(batch);
    }

    [Fact]
    public async Task PublishAsync_MultipleBatches_AllWritten()
    {
        var channel = Channel.CreateUnbounded<AuditLogBatch>();
        ChannelAuditLogPublisher publisher = new(channel);

        for (int i = 0; i < 3; i++)
        {
            AuditLogBatch batch = new(
                Timestamp: DateTimeOffset.UtcNow,
                UserId: $"user-{i}",
                UserName: null,
                Category: AuditLogCategory.DataMutation,
                IpAddress: null,
                UserAgent: null,
                TenantId: null,
                CorrelationId: null,
                EntityChanges: []);

            await publisher.PublishAsync(batch, TestContext.Current.CancellationToken);
        }

        int count = 0;
        while (channel.Reader.TryRead(out _))
        {
            count++;
        }

        count.ShouldBe(3);
    }
}
