using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System.Text;

namespace Aeroverra.Lib.Queues
{
    /// <summary>
    /// RabbitMQ implementation of <see cref="IQueuePublisher"/>. Holds a single long-lived
    /// connection per process (opened lazily on first publish) and creates a fresh channel
    /// per publish. Auto-recovery (enabled by default in RabbitMQ.Client) handles drops, so
    /// once the first connect succeeds the connection stays usable across blips.
    /// </summary>
    internal class QueuePublisher : IQueuePublisher, IAsyncDisposable
    {
        // Async lazy — connection opens on first publish, then sticks around for the
        // lifetime of the singleton. RabbitMQ.Client's auto-recovery handles drops.
        private readonly Lazy<Task<IConnection>> _lazyConnection;

        public QueuePublisher(IOptions<RabbitMQOptions> options, ILogger<QueuePublisher> logger)
        {
            var rabbit = options.Value;
            _lazyConnection = new Lazy<Task<IConnection>>(() =>
            {
                var factory = RabbitMQConnectionHelpers.CreateFactory(rabbit);
                return RabbitMQConnectionHelpers.ConnectWithRetryAsync(factory, logger, CancellationToken.None);
            });
        }

        // No queue declaration here. Topology is owned by IQueueTopologyApplier and applied
        // once at startup; the publisher trusts queues already exist. Publishing to a
        // non-existent queue with mandatory:true returns the message as unroutable, which
        // is the right failure mode — declares belong in topology config, not in publish.
        public Task PublishAsync<T>(string queueName, T payload, string? id = null)
        {
            var messageId = id ?? Guid.NewGuid().ToString();
            var envelope = new Envelope<T>
            {
                Id = messageId,
                AttemptCount = 0,
                TimePublished = DateTimeOffset.UtcNow,
                Payload = payload,
            };
            return PublishRawAsync(queueName, messageId, envelope);
        }

        public async Task PublishRawAsync<TMessage>(string queueName, string id, TMessage message)
        {
            var connection = await _lazyConnection.Value;

            await using var channel = await connection.CreateChannelAsync();

            var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                CorrelationId = id,
            };

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queueName,
                mandatory: true,
                basicProperties: properties,
                body: body);
        }

        /// <summary>
        /// Closes the underlying connection if it was ever opened. The DI container calls
        /// this on shutdown for singleton services that implement <see cref="IAsyncDisposable"/>.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_lazyConnection.IsValueCreated)
            {
                var connection = await _lazyConnection.Value;
                await connection.DisposeAsync();
            }
        }
    }
}
