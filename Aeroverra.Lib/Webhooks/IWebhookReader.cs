using Microsoft.AspNetCore.Http;

namespace Aeroverra.Lib.Webhooks
{
    /// <summary>
    /// Handles reading an http request body, headers and query parameters and converting them into a <see cref="Webhook"/> object. This is used by the <see cref="IWebhookService"/> to read incoming webhooks from various providers.
    /// </summary>
    public interface IWebhookReader
    {
        /// <summary>
        /// Reads a webhook from the HTTP request asynchronously.
        /// </summary>
        /// <param name="Request">The HTTP request containing the webhook data.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the webhook read from the
        /// request including its headers and query parameters.</returns>
        Task<Webhook> ReadAsync(HttpRequest Request, CancellationToken cancellationToken);
    }
}
