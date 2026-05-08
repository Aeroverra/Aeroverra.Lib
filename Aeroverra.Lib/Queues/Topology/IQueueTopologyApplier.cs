namespace Aeroverra.Lib.Queues.Topology
{
    /// <summary>
    /// Applies a <see cref="QueueTopology"/> against a broker — declares exchanges, queues,
    /// and bindings. Implementations are broker-specific (see <c>RabbitMQTopologyApplier</c>).
    /// Idempotent: re-applying the same topology is a no-op.
    /// </summary>
    public interface IQueueTopologyApplier
    {
        /// <summary>
        /// Declares everything in <paramref name="topology"/> against the broker. Order is
        /// exchanges → queues → bindings so that queue args referencing exchanges resolve
        /// and bindings find both ends already declared.
        /// </summary>
        Task ApplyAsync(QueueTopology topology, CancellationToken cancellationToken = default);
    }
}
