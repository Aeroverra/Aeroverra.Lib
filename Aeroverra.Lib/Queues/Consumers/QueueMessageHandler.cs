using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client.Events;

namespace Aeroverra.Lib.Queues.Consumers
{
    /// <summary>
    /// Strongly-typed handler base. Owns the boilerplate that every handler would otherwise
    /// repeat: JSON deserialization into <see cref="Envelope{T}"/>, attempt-cap enforcement,
    /// and republish-on-Retry with attempt-counter increment.
    ///
    /// Implementers override <see cref="QueueName"/>, optionally <see cref="MaxAttempts"/>,
    /// and <see cref="HandleAsync(Envelope{T}, BasicDeliverEventArgs, CancellationToken)"/>.
    /// Handler code is pure business logic + an outcome — no parsing, no null checks, no
    /// counter tracking, no manual republish.
    ///
    /// Outcomes:
    /// <list type="bullet">
    ///   <item><see cref="QueueHandlerResult.Success"/> — base returns Success, host acks.</item>
    ///   <item><see cref="QueueHandlerResult.Fail(bool, string)"/> — base passes through, host nacks.</item>
    ///   <item><see cref="QueueHandlerResult.Retry(string)"/> — base republishes envelope with
    ///   <c>AttemptCount + 1</c>, then returns Success to the host so the original is acked.</item>
    /// </list>
    /// </summary>
    public abstract class QueueMessageHandler<T>(IQueuePublisher publisher, ILogger logger) : IQueueMessageHandler
    {
        /// <summary>
        /// The queue this handler subscribes to.
        /// </summary>
        public abstract string QueueName { get; }

        /// <summary>
        /// Maximum delivery attempts before <see cref="HandleAsync"/> short-circuits with
        /// <see cref="QueueHandlerResult.Fail(bool, string)"/>. Counts the current delivery —
        /// e.g. <c>MaxAttempts = 5</c> with <c>AttemptCount = 5</c> on arrival short-circuits
        /// without invoking the derived class.
        /// </summary>
        public virtual int MaxAttempts => 5;

        async Task<QueueHandlerResult> IQueueMessageHandler.HandleAsync(string body, BasicDeliverEventArgs deliverArgs, CancellationToken cancellationToken)
        {
            Envelope<T>? envelope;
            try
            {
                envelope = JsonConvert.DeserializeObject<Envelope<T>>(body);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Failed to deserialize Envelope<{Type}> on queue {Queue}: {Body}",
                    typeof(T).Name, QueueName, body);
                return QueueHandlerResult.Fail(requeue: false, reason: "Malformed envelope JSON");
            }

            if (envelope is null || string.IsNullOrWhiteSpace(envelope.Id))
            {
                logger.LogError("Empty envelope or missing Id on queue {Queue}: {Body}", QueueName, body);
                return QueueHandlerResult.Fail(requeue: false, reason: "Empty envelope or missing Id");
            }

            if (envelope.AttemptCount >= MaxAttempts)
            {
                logger.LogError("Message {Id} on {Queue} exceeded MaxAttempts={Max} (AttemptCount={Count}) — failing without retry",
                    envelope.Id, QueueName, MaxAttempts, envelope.AttemptCount);
                return QueueHandlerResult.Fail(requeue: false, reason: $"Max attempts ({MaxAttempts}) exceeded");
            }

            QueueHandlerResult result;
            try
            {
                result = await HandleAsync(envelope, deliverArgs, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Surface as a Fail rather than letting it bubble — the host has its own
                // safety catch but this gives us envelope-aware logging.
                logger.LogError(ex, "Unhandled exception in {Handler} for {Id} (attempt {Attempt}/{Max})",
                    GetType().Name, envelope.Id, envelope.AttemptCount + 1, MaxAttempts);
                throw; // let host catch handle the nack — keeps the host's poison-pill semantics intact
            }

            if (result.IsRetry)
            {
                var next = envelope with { AttemptCount = envelope.AttemptCount + 1 };
                await publisher.PublishRawAsync(QueueName, envelope.Id, next);
                logger.LogWarning("Retry requested for {Id} on {Queue}: {Reason}. Republished with AttemptCount={Next}/{Max}.",
                    envelope.Id, QueueName, result.Reason, next.AttemptCount, MaxAttempts);
                return QueueHandlerResult.Success();
            }

            return result;
        }

        /// <summary>
        /// Handle a single delivery. Return <see cref="QueueHandlerResult.Success"/> when
        /// done, <see cref="QueueHandlerResult.Retry"/> for transient failures (the framework
        /// will republish with AttemptCount + 1 and ack the original), or
        /// <see cref="QueueHandlerResult.Fail"/> for poison messages.
        /// </summary>
        protected abstract Task<QueueHandlerResult> HandleAsync(
            Envelope<T> envelope,
            BasicDeliverEventArgs deliverArgs,
            CancellationToken cancellationToken);
    }
}
