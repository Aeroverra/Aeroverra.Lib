namespace Aeroverra.Lib.Webhooks
{
    /// <summary>
    /// Defines a contract for validating webhooks from a specific provider used by the <see cref="IWebhookService"/>
    /// </summary>
    public interface IWebhookValidator
    {
        /// <summary>
        /// Validates a webhook is originating from a trusted source
        /// </summary>
        /// <param name="webhook">The webhook to validate.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>True if the webhook is valid, otherwise false.</returns>
        Task<bool> ValidateAsync(Webhook webhook, CancellationToken cancellationToken = default);

        /// <summary>
        /// The provider this validator is for. This should be unique across all validators. If multiple validators are found for the same provider, only the last one will be used and a warning will be logged.
        /// </summary>
        string ProviderName { get; }
    }

    /// <summary>
    /// Strongly-typed base for <see cref="IWebhookValidator"/> implementations whose provider list is captured by an enum.
    /// Eliminates string typos at the call site by deriving <see cref="ProviderName"/> from <see cref="Provider"/>.
    /// </summary>
    /// <typeparam name="TEnum">The consumer-defined enum that enumerates webhook providers.</typeparam>
    public abstract class WebhookValidator<TEnum> : IWebhookValidator where TEnum : struct, Enum
    {
        /// <summary>
        /// The provider this validator is for.
        /// </summary>
        public abstract TEnum Provider { get; }

        /// <inheritdoc />
        public string ProviderName => Provider.ToString();

        /// <inheritdoc />
        public abstract Task<bool> ValidateAsync(Webhook webhook, CancellationToken cancellationToken = default);
    }
}
