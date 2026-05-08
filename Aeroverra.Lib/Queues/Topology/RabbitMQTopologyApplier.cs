using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aeroverra.Lib.Queues.Topology
{
    /// <summary>
    /// RabbitMQ-specific implementation of <see cref="IQueueTopologyApplier"/>. Opens a
    /// short-lived connection (with retry), declares the topology, then closes — runs once
    /// per host startup via <see cref="QueueTopologyHostedService"/>.
    /// </summary>
    internal sealed class RabbitMQTopologyApplier(
        IOptions<RabbitMQOptions> options,
        ILogger<RabbitMQTopologyApplier> logger) : IQueueTopologyApplier
    {
        private readonly RabbitMQOptions _rabbit = options.Value;

        public async Task ApplyAsync(QueueTopology topology, CancellationToken cancellationToken = default)
        {
            // Short-lived connection — topology application happens once per host startup,
            // so we don't share the publisher's or consumer's long-lived connection.
            var factory = RabbitMQConnectionHelpers.CreateFactory(_rabbit);
            await using var connection = await RabbitMQConnectionHelpers.ConnectWithRetryAsync(factory, logger, cancellationToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            // Order matters: exchanges before queues (so x-dead-letter-exchange refs resolve),
            // queues before bindings (bindings reference both ends).
            foreach (var exchange in topology.Exchanges)
            {
                await channel.ExchangeDeclareAsync(
                    exchange: exchange.Name,
                    type: exchange.Type,
                    durable: exchange.Durable,
                    autoDelete: exchange.AutoDelete,
                    arguments: exchange.Arguments,
                    cancellationToken: cancellationToken);
                logger.LogInformation("Declared exchange {Name} ({Type})", exchange.Name, exchange.Type);
            }

            foreach (var queue in topology.Queues)
            {
                await channel.QueueDeclareAsync(
                    queue: queue.Name,
                    durable: queue.Durable,
                    exclusive: queue.Exclusive,
                    autoDelete: queue.AutoDelete,
                    arguments: queue.Arguments,
                    cancellationToken: cancellationToken);
                logger.LogInformation("Declared queue {Name}", queue.Name);
            }

            foreach (var binding in topology.Bindings)
            {
                await channel.QueueBindAsync(
                    queue: binding.Destination,
                    exchange: binding.Source,
                    routingKey: binding.RoutingKey,
                    arguments: binding.Arguments,
                    cancellationToken: cancellationToken);
                logger.LogInformation("Bound queue {Queue} ← exchange {Exchange} (key='{Key}')",
                    binding.Destination, binding.Source, binding.RoutingKey);
            }
        }
    }
}
