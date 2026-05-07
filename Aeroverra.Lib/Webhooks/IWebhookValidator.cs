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
}
