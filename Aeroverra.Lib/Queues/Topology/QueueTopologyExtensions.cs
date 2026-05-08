using Aeroverra.Lib.Queues.Topology.Definitions;
using RabbitMQ.Client;

namespace Aeroverra.Lib.Queues.Topology
{
    /// <summary>
    /// Composable shortcuts for common patterns. Each method just appends to the topology —
    /// no broker calls. Add new patterns here as they emerge (priority queues, TTL retry
    /// queues, lazy queues, quorum queues, etc.).
    /// </summary>
    public static class QueueTopologyExtensions
    {
        public const string DefaultDlxSuffix = ".dlx";
        public const string DefaultDlqSuffix = ".deadletter";

        /// <summary>
        /// Adds a plain durable queue. Equivalent to <c>topology.AddQueue(new QueueDefinition(name))</c>.
        /// </summary>
        public static QueueTopology AddSimpleQueue(this QueueTopology topology, string name)
            => topology.AddQueue(new QueueDefinition(name));

        /// <summary>
        /// Adds a queue plus a fanout DLX and a DLQ bound to it. Messages dead-lettered from
        /// the main queue (handler returns <c>Fail(requeue: false)</c>, throws, hits retry cap,
        /// or expires via TTL) get routed to the DLQ for inspection or replay.
        /// </summary>
        public static QueueTopology AddQueueWithDeadLetter(
            this QueueTopology topology,
            string queueName,
            string? deadLetterExchange = null,
            string? deadLetterQueue = null)
        {
            deadLetterExchange ??= queueName + DefaultDlxSuffix;
            deadLetterQueue ??= queueName + DefaultDlqSuffix;

            return topology
                .AddExchange(new ExchangeDefinition(deadLetterExchange, ExchangeType.Fanout))
                .AddQueue(new QueueDefinition(deadLetterQueue))
                .AddBinding(new BindingDefinition(deadLetterExchange, deadLetterQueue))
                .AddQueue(new QueueDefinition(
                    queueName,
                    Arguments: new Dictionary<string, object?>
                    {
                        ["x-dead-letter-exchange"] = deadLetterExchange,
                    }));
        }
    }
}
