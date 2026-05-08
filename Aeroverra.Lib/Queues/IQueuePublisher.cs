namespace Aeroverra.Lib.Queues
{
    /// <summary>
    /// Publishes messages to a RabbitMQ queue. Registered as a singleton via
    /// <c>AddRabbitMQPublisher</c> (and transitively via <c>AddRabbitMQConsumerHost</c>).
    /// Supports two paths: the standard envelope-aware <see cref="PublishAsync{T}"/>, and a
    /// raw escape hatch <see cref="PublishRawAsync{TMessage}"/> for non-envelope flows.
    /// </summary>
    public interface IQueuePublisher
    {
        /// <summary>
        /// Wraps <paramref name="payload"/> in an <see cref="Envelope{T}"/> with metadata
        /// (id, attempt count = 0, publish time = now) and publishes it. Standard path —
        /// pairs with consumers extending <c>QueueMessageHandler&lt;T&gt;</c>, which will
        /// see the deserialized envelope and get framework-managed retry support.
        /// </summary>
        /// <param name="id">Optional correlation id. If null, a fresh GUID is generated.</param>
        Task PublishAsync<T>(string queueName, T payload, string? id = null);

        /// <summary>
        /// Low-level escape hatch — publishes <paramref name="message"/> exactly as given,
        /// no envelope wrapping. Use only when integrating with non-envelope producers /
        /// consumers, or when the framework's typed handler base needs to republish a
        /// pre-existing envelope verbatim (with an incremented attempt counter).
        /// </summary>
        Task PublishRawAsync<TMessage>(string queueName, string id, TMessage message);
    }
}
