using RabbitMQ.Client.Events;

namespace Aeroverra.Lib.Queues.Consumers
{
    /// <summary>
    /// Contract the consumer host uses to dispatch deliveries. Most handlers should extend
    /// the typed base <c>QueueMessageHandler&lt;T&gt;</c> instead of implementing this directly —
    /// the base owns envelope parsing, retry-cap enforcement, and republish-on-Retry. Implement
    /// this interface directly only when consuming bare bodies that aren't wrapped in an
    /// <see cref="Envelope{T}"/>.
    /// </summary>
    public interface IQueueMessageHandler
    {
        /// <summary>
        /// The queue name this handler binds to. Multiple handlers can be registered;
        /// the consumer host routes each message to the handler whose QueueName matches.
        /// </summary>
        string QueueName { get; }

        /// <summary>
        /// Process a single delivery. Return a <see cref="QueueHandlerResult"/> describing
        /// the outcome — the host translates Success → ack, Fail → nack (with or without
        /// requeue per the result), and Retry → nack-without-requeue (since raw handlers
        /// have no envelope to update; for proper Retry use <c>QueueMessageHandler&lt;T&gt;</c>).
        /// </summary>
        Task<QueueHandlerResult> HandleAsync(string body, BasicDeliverEventArgs deliverArgs, CancellationToken cancellationToken);
    }
}
