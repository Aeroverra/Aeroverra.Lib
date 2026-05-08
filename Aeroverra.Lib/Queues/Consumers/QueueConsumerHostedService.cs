using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Text;

namespace Aeroverra.Lib.Queues.Consumers
{
    /// <summary>
    /// Hosted service that opens one long-lived RabbitMQ connection and starts N consumers
    /// per queue (per <see cref="QueueProcessorOptions"/>), each on its own channel with
    /// its own prefetch. Routes every delivery to the matching <see cref="IQueueMessageHandler"/>
    /// (looked up by <c>QueueName</c>) inside a fresh DI scope.
    ///
    /// Relies on RabbitMQ.Client's built-in auto-recovery for connection drops; handles
    /// startup connect failures via <see cref="RabbitMQConnectionHelpers.ConnectWithRetryAsync"/>
    /// so the worker can boot before the broker is reachable.
    /// </summary>
    public class QueueConsumerHostedService(
        IOptions<RabbitMQOptions> rabbitOptions,
        IOptions<QueueProcessorOptions> processorOptions,
        IServiceProvider serviceProvider,
        ILogger<QueueConsumerHostedService> logger) : BackgroundService
    {
        private readonly RabbitMQOptions _rabbit = rabbitOptions.Value;
        private readonly QueueProcessorOptions _processor = processorOptions.Value;

        private IConnection? _connection;
        private readonly List<IChannel> _channels = new();

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // RabbitMQ.Client's built-in auto-recovery handles connection drops:
            //   AutomaticRecoveryEnabled = true (default) — reconnects every NetworkRecoveryInterval (5s default)
            //   TopologyRecoveryEnabled = true (default)  — re-declares queues/exchanges and re-registers consumers
            // The IConnection/IChannel references in this host stay valid across recovery,
            // so we don't have to recreate anything ourselves.
            var factory = RabbitMQConnectionHelpers.CreateFactory(_rabbit);

            _connection = await RabbitMQConnectionHelpers.ConnectWithRetryAsync(factory, logger, stoppingToken);
            _connection.ConnectionShutdownAsync += OnConnectionShutdownAsync;

            if (_processor.Queues.Count == 0)
            {
                logger.LogWarning("No queues configured under {Section}:Queues — host will idle", QueueProcessorOptions.SectionName);
            }

            foreach (var (queueName, queueOpts) in _processor.Queues)
            {
                if (queueOpts.ConsumerCount <= 0)
                {
                    logger.LogInformation("Queue {Queue} has ConsumerCount=0 — skipping", queueName);
                    continue;
                }

                for (var i = 0; i < queueOpts.ConsumerCount; i++)
                {
                    await StartConsumerAsync(queueName, queueOpts, i, stoppingToken);
                }
            }

            // Park until shutdown — consumers run on their own callbacks.
            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // expected on shutdown
            }
        }

        private async Task StartConsumerAsync(string queueName, QueueConsumerOptions queueOpts, int index, CancellationToken stoppingToken)
        {
            var consumerTag = $"{Environment.MachineName}-{queueName}-{index}";
            var channel = await _connection!.CreateChannelAsync(cancellationToken: stoppingToken);

            channel.ChannelShutdownAsync += (_, args) =>
            {
                // Logging only — the library auto-recovers the channel and re-registers the consumer.
                // In-flight handlers continue running; their eventual ack/nack will fail (logged in
                // HandleAsync) but the broker will have already redelivered the message elsewhere.
                logger.LogWarning(
                    "[{ConsumerTag}] Channel shutdown — initiator={Initiator} code={Code} text={Text}. Auto-recovery will re-register the consumer.",
                    consumerTag, args.Initiator, args.ReplyCode, args.ReplyText);
                return Task.CompletedTask;
            };

            // No queue declaration here — topology is owned by IQueueTopologyApplier and
            // applied at host startup before this service runs. If a queue is missing,
            // BasicConsumeAsync will fail with NOT_FOUND, which is the right signal that
            // topology config is incomplete.

            // Per-consumer prefetch — RabbitMQ won't push more than this many unacked messages
            // to this consumer at a time. Keeps fast consumers from hoarding work.
            await channel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: queueOpts.PrefetchCount,
                global: false,
                cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += (_, ea) => HandleAsync(queueName, channel, ea, consumerTag, stoppingToken);

            await channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: false,
                consumerTag: consumerTag,
                noLocal: false,
                exclusive: false,
                arguments: null,
                consumer: consumer,
                cancellationToken: stoppingToken);

            _channels.Add(channel);
            logger.LogInformation("Consumer {ConsumerTag} bound to queue {Queue} (prefetch={Prefetch})", consumerTag, queueName, queueOpts.PrefetchCount);
        }

        private Task OnConnectionShutdownAsync(object? sender, ShutdownEventArgs args)
        {
            // Logging only — the library auto-reconnects every NetworkRecoveryInterval (5s default)
            // until success, then re-declares topology and re-registers consumers.
            logger.LogWarning(
                "RabbitMQ connection shutdown — initiator={Initiator} code={Code} text={Text}. Auto-recovery will reconnect.",
                args.Initiator, args.ReplyCode, args.ReplyText);
            return Task.CompletedTask;
        }

        private async Task HandleAsync(string queueName, IChannel channel, BasicDeliverEventArgs ea, string consumerTag, CancellationToken stoppingToken)
        {
            string? correlationId = null;

            try
            {
                correlationId = ea.BasicProperties?.CorrelationId;
                var body = Encoding.UTF8.GetString(ea.Body.Span);

                using var scope = serviceProvider.CreateScope();
                var handler = scope.ServiceProvider
                    .GetServices<IQueueMessageHandler>()
                    .FirstOrDefault(h => string.Equals(h.QueueName, queueName, StringComparison.Ordinal));

                if (handler is null)
                {
                    logger.LogError("[{ConsumerTag}] No IQueueMessageHandler registered for queue {Queue} — dead-lettering", consumerTag, queueName);
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
                    return;
                }

                var result = await handler.HandleAsync(body, ea, stoppingToken);

                if (result.IsSuccess)
                {
                    await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                    logger.LogInformation("[{ConsumerTag}] ACK {CorrelationId}", consumerTag, correlationId);
                }
                else
                {
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: result.Requeue, cancellationToken: stoppingToken);
                    logger.LogWarning("[{ConsumerTag}] NACK {CorrelationId} requeue={Requeue} reason={Reason}",
                        consumerTag, correlationId, result.Requeue, result.Reason);
                }
            }
            catch (AlreadyClosedException)
            {
                // Channel/connection died between receive and ack/nack. The broker has already
                // redelivered (or will, once recovery completes) — handlers must be idempotent.
                logger.LogWarning("[{ConsumerTag}] Channel was closed before ack/nack of {CorrelationId} — broker will redeliver", consumerTag, correlationId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("[{ConsumerTag}] Handler cancelled due to host shutdown for {CorrelationId} — broker will redeliver", consumerTag, correlationId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[{ConsumerTag}] Unhandled exception processing {CorrelationId} — nacking without requeue to avoid poison loop",
                    consumerTag, correlationId);
                try
                {
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
                }
                catch (AlreadyClosedException)
                {
                    // Channel died while we were trying to nack the original failure. Broker will redeliver.
                }
                catch (Exception nackEx)
                {
                    logger.LogError(nackEx, "[{ConsumerTag}] Failed to nack message {CorrelationId}", consumerTag, correlationId);
                }
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);

            if (_connection is not null)
            {
                _connection.ConnectionShutdownAsync -= OnConnectionShutdownAsync;
            }

            foreach (var channel in _channels)
            {
                try { await channel.CloseAsync(cancellationToken); }
                catch (Exception ex) { logger.LogWarning(ex, "Error closing channel"); }
                await channel.DisposeAsync();
            }
            _channels.Clear();

            if (_connection is not null)
            {
                try { await _connection.CloseAsync(cancellationToken); }
                catch (Exception ex) { logger.LogWarning(ex, "Error closing connection"); }
                await _connection.DisposeAsync();
                _connection = null;
            }
        }
    }
}
