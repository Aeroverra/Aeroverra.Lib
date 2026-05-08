namespace Aeroverra.Lib.Queues.Consumers
{
    /// <summary>
    /// Discriminator for <see cref="QueueHandlerResult"/>. Tells the host (and the typed
    /// handler base) what action to take after a handler returns.
    /// </summary>
    public enum QueueHandlerOutcome
    {
        /// <summary>
        /// Handled successfully. Host acks the delivery.
        /// </summary>
        Success,

        /// <summary>
        /// Handling failed. Host nacks (with or without requeue per <see cref="QueueHandlerResult.Requeue"/>).
        /// </summary>
        Fail,

        /// <summary>
        /// Transient failure — request a retry with attempt tracking. Only meaningful when
        /// produced by a <c>QueueMessageHandler&lt;T&gt;</c> implementation: the typed base
        /// republishes the envelope with <c>AttemptCount + 1</c>, then acks the original.
        /// Raw <see cref="IQueueMessageHandler"/> handlers returning Retry are treated by
        /// the host as <c>Fail(requeue: false)</c> since the host has no envelope to update.
        /// </summary>
        Retry,
    }

    /// <summary>
    /// Outcome of a single message handler invocation. Use the static factories
    /// (<see cref="Success"/>, <see cref="Fail"/>, <see cref="Retry"/>) — the positional
    /// constructor is for the factories' use.
    /// </summary>
    public readonly record struct QueueHandlerResult(QueueHandlerOutcome Outcome, bool Requeue, string? Reason)
    {
        /// <summary>
        /// True when <see cref="Outcome"/> is <see cref="QueueHandlerOutcome.Success"/>.
        /// </summary>
        public bool IsSuccess => Outcome == QueueHandlerOutcome.Success;

        /// <summary>
        /// True when <see cref="Outcome"/> is <see cref="QueueHandlerOutcome.Fail"/>.
        /// </summary>
        public bool IsFail => Outcome == QueueHandlerOutcome.Fail;

        /// <summary>
        /// True when <see cref="Outcome"/> is <see cref="QueueHandlerOutcome.Retry"/>.
        /// </summary>
        public bool IsRetry => Outcome == QueueHandlerOutcome.Retry;

        /// <summary>
        /// Message handled successfully — host will ack the delivery.
        /// </summary>
        public static QueueHandlerResult Success() => new(QueueHandlerOutcome.Success, false, null);

        /// <summary>
        /// Reject the message. Set <paramref name="requeue"/> to true for a broker-level
        /// requeue (instant redelivery, no attempt tracking, can loop). Set false for
        /// poison messages — the broker drops or dead-letters depending on queue config.
        /// For capped, app-managed retries with attempt tracking, prefer <see cref="Retry"/>.
        /// </summary>
        public static QueueHandlerResult Fail(bool requeue, string reason) => new(QueueHandlerOutcome.Fail, requeue, reason);

        /// <summary>
        /// Transient failure — the framework republishes the envelope with an incremented
        /// attempt counter (until <c>MaxAttempts</c>) and acks the original. Only honored
        /// inside <c>QueueMessageHandler&lt;T&gt;</c>; see <see cref="QueueHandlerOutcome.Retry"/>.
        /// </summary>
        public static QueueHandlerResult Retry(string reason) => new(QueueHandlerOutcome.Retry, false, reason);
    }
}
