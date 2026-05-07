using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Aeroverra.Lib.Webhooks
{
    /// <inheritdoc/>
    internal sealed class DefaultWebhookService : IWebhookService
    {
        private readonly ILogger<DefaultWebhookService> _logger;
        private readonly IWebhookHandler _webhookHandler;
        private readonly IWebhookReader _webhookReader;
        private readonly IDictionary<string, IWebhookValidator> _webhookValidatorMap = new Dictionary<string, IWebhookValidator>();

        public DefaultWebhookService(
            ILogger<DefaultWebhookService> logger,
            IWebhookHandler webhookHandler,
            IWebhookReader webhookReader,
            IEnumerable<IWebhookValidator> webhookValidators)
        {
            _logger = logger;
            _webhookHandler = webhookHandler;
            _webhookReader = webhookReader;
            MapWebhookValidators(webhookValidators);
        }

        /// <inheritdoc />
        public async Task<(Webhook webhook, bool isValid)> ReadAndValidateAsync(string provider, HttpRequest Request, CancellationToken cancellationToken = default)
        {
            var webhook = await ReadAsync(Request, cancellationToken);
            var isValid = await ValidateAsync(provider, webhook, cancellationToken);
            return (webhook, isValid);
        }

        /// <inheritdoc />
        public async Task<Webhook> ReadAsync(HttpRequest request, CancellationToken cancellationToken = default)
        {
            var webhook = await _webhookReader.ReadAsync(request, cancellationToken);
            await _webhookHandler.OnAfterReadAsync(webhook, cancellationToken);
            return webhook;
        }

        /// <inheritdoc />
        public async Task<bool> ValidateAsync(string provider, Webhook webhook, CancellationToken cancellationToken = default)
        {
            var isValid = false;

            try
            {
                var validator = _webhookValidatorMap[provider];
                isValid =  await validator.ValidateAsync(webhook, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while validating a webhook for provider '{Provider}'.", provider);
            }

            await _webhookHandler.OnAfterValidateAsync(provider, isValid, webhook, cancellationToken);
            return isValid;
        }

        private void MapWebhookValidators(IEnumerable<IWebhookValidator> webhookValidators)
        {
            foreach (var validator in webhookValidators)
            {
                var provider = validator.ProviderName;

                if (string.IsNullOrWhiteSpace(provider))
                {
                    _logger.LogWarning("A webhook validator was registered without a ProviderName and will be skipped.");
                    continue;
                }

                // If the key already exists, we log that we are overriding it.
                if (_webhookValidatorMap.ContainsKey(provider))
                {
                    _logger.LogTrace("Overriding existing webhook validator for provider '{Provider}'.", provider);
                }

                // The dictionary value is always overwritten. 
                // Because DI provides the collection in order of registration, 
                // the last one registered will be the final one stored here.
                _webhookValidatorMap[provider] = validator;
            }
        }
    }
}
