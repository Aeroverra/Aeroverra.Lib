namespace Aeroverra.Lib.Webhooks
{
    /// <summary>
    /// Defines methods called by the <see cref="IWebhookService"/> after reading and validating a webhook. This allows you to perform custom actions such as logging, metrics, or triggering other processes based on the webhook data and validation results.
    /// </summary>
    public interface IWebhookHandler
    {
        /// <summary>
        /// Called after a webhook has been read from the HTTP request.
        /// </summary>
        /// <param name="webhook">The webhook that was read.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        Task OnAfterReadAsync(Webhook webhook, CancellationToken cancellationToken);

        /// <summary>
        /// Called after webhook validation completes.
        /// </summary>
        /// <param name="provider">The webhook provider identifier.</param>
        /// <param name="isValid">Indicates whether the webhook validation succeeded.</param>
        /// <param name="webhook">The webhook that was validated.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task OnAfterValidateAsync(string provider, bool isValid, Webhook webhook, CancellationToken cancellationToken);
    }
}
