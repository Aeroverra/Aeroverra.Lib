using Aeroverra.Lib.Webhooks;

namespace Aeroverra.Lib
{
    /// <summary>
    /// Used when no custom <see cref="IWebhookHandler"/> is registered. This allows the <see cref="IWebhookService"/> 
    /// </summary>
    internal sealed class DefaultWebhookHandler : IWebhookHandler
    {
        public Task OnAfterReadAsync(Webhook webhook, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task OnAfterValidateAsync(string provider, bool isValid, Webhook webhook, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
