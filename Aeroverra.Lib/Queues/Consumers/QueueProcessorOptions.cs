namespace Aeroverra.Lib.Queues.Consumers
{
    /// <summary>
    /// Consumer-host configuration. Bound from the <c>QueueProcessor</c> section of
    /// <c>IConfiguration</c> by <c>AddRabbitMQConsumerHost</c>.
    /// </summary>
    public class QueueProcessorOptions
    {
        /// <summary>
        /// The configuration section name these options bind from.
        /// </summary>
        public const string SectionName = "QueueProcessor";

        /// <summary>
        /// Per-queue consumer settings, keyed by RabbitMQ queue name.
        /// </summary>
        public Dictionary<string, QueueConsumerOptions> Queues { get; set; } = new();
    }

    /// <summary>
    /// Settings for a single queue's consumer pool inside the consumer host.
    /// </summary>
    public class QueueConsumerOptions
    {
        /// <summary>
        /// Number of independent consumers (each with its own channel) to attach to this queue.
        /// Increase to scale processing within a single instance.
        /// </summary>
        public int ConsumerCount { get; set; } = 1;

        /// <summary>
        /// Per-consumer prefetch count — limits how many unacked messages each consumer will hold at once.
        /// </summary>
        public ushort PrefetchCount { get; set; } = 10;
    }
}
