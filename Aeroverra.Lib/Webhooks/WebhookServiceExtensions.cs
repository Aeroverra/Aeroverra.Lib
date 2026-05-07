using Microsoft.AspNetCore.Http;

namespace Aeroverra.Lib.Webhooks
{
    /// <summary>
    /// Strongly-typed convenience overloads for <see cref="IWebhookService"/>. The lib stores providers as strings
    /// internally; these wrappers let consumers pass any enum value and avoid stringly-typed call sites.
    /// </summary>
    public static class WebhookServiceExtensions
    {
        /// <inheritdoc cref="IWebhookService.ReadAndValidateAsync(string, HttpRequest, CancellationToken)" />
        public static Task<(Webhook webhook, bool isValid)> ReadAndValidateAsync<TEnum>(
            this IWebhookService service,
            TEnum provider,
            HttpRequest request,
            CancellationToken cancellationToken = default)
            where TEnum : struct, Enum
            => service.ReadAndValidateAsync(provider.ToString(), request, cancellationToken);

        /// <inheritdoc cref="IWebhookService.ValidateAsync(string, Webhook, CancellationToken)" />
        public static Task<bool> ValidateAsync<TEnum>(
            this IWebhookService service,
            TEnum provider,
            Webhook webhook,
            CancellationToken cancellationToken = default)
            where TEnum : struct, Enum
            => service.ValidateAsync(provider.ToString(), webhook, cancellationToken);
    }
}
