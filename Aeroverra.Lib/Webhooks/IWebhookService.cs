using Microsoft.AspNetCore.Http;

namespace Aeroverra.Lib.Webhooks
{
    /// <summary>
    /// Provides an easy way to read and validate incoming webhook requests from various providers.
    /// </summary>
    public interface IWebhookService
    {
        /// <summary>
        /// Reads and validates a webhook from the HTTP request for the specified provider. 
        /// </summary>
        /// <param name="provider">The webhook provider identifier.</param>
        /// <param name="Request">The HTTP request containing the webhook payload.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A tuple containing the parsed webhook and a boolean indicating whether the validation was successful.</returns>
        Task<(Webhook webhook, bool isValid)> ReadAndValidateAsync(string provider, HttpRequest Request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads a webhook from the HTTP request asynchronously.   
        /// </summary>
        /// <param name="Request">The HTTP request containing the webhook data.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the webhook read from the
        /// request including its headers and query parameters.</returns>
        Task<Webhook> ReadAsync(HttpRequest Request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates a webhook for the specified provider. 
        /// </summary>
        /// <param name="provider">The provider name or identifier.</param>
        /// <param name="webhook">The webhook to validate.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains <see langword="true"/> if the
        /// webhook is valid; otherwise, <see langword="false"/>.</returns>
        Task<bool> ValidateAsync(string provider, Webhook webhook, CancellationToken cancellationToken = default);
    }
}