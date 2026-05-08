using Microsoft.Extensions.Hosting;

namespace Aeroverra.Lib.Queues.Topology
{
    /// <summary>
    /// Applies the configured topology to RabbitMQ during host startup, before any other
    /// queue-related hosted service binds consumers or publishes. Idempotent — re-applying
    /// the same topology is a no-op.
    /// </summary>
    internal sealed class QueueTopologyHostedService(
        IQueueTopologyApplier applier,
        QueueTopology topology) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
            => applier.ApplyAsync(topology, cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
