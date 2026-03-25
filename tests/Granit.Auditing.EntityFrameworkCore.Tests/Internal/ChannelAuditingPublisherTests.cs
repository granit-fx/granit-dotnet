using System.Threading.Channels;
using Granit.Auditing.Domain;
using Granit.Auditing.EntityFrameworkCore.Internal.Services;
using Granit.Auditing.Messages;
using Shouldly;
using Xunit;

namespace Granit.Auditing.EntityFrameworkCore.Tests.Internal;

public sealed class ChannelAuditingPublisherTests
{
    [Fact]
    public async Task PublishAsync_WritesBatchToChannel()
    {
        var channel = Channel.CreateUnbounded<AuditingBatch>();
        ChannelAuditingPublisher publisher = new(channel);

        AuditingBatch batch = new(
            Timestamp: DateTimeOffset.UtcNow,
            UserId: "user-1",
            UserName: "Test",
            Category: AuditCategory.DataMutation,
            IpAddress: null,
            UserAgent: null,
            TenantId: null,
            CorrelationId: null,
            EntityChanges: []);

        await publisher.PublishAsync(batch, TestContext.Current.CancellationToken);

        bool read = channel.Reader.TryRead(out AuditingBatch? received);
        read.ShouldBeTrue();
        received.ShouldBe(batch);
    }

    [Fact]
    public async Task PublishAsync_MultipleBatches_AllWritten()
    {
        var channel = Channel.CreateUnbounded<AuditingBatch>();
        ChannelAuditingPublisher publisher = new(channel);

        for (int i = 0; i < 3; i++)
        {
            AuditingBatch batch = new(
                Timestamp: DateTimeOffset.UtcNow,
                UserId: $"user-{i}",
                UserName: null,
                Category: AuditCategory.DataMutation,
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
