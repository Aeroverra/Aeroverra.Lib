using Aeroverra.Lib.Webhooks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aeroverra.Lib.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAeroverraLibServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.TryAddSingleton<IWebhookService, DefaultWebhookService>();
            services.TryAddSingleton<IWebhookReader, DefaultWebhookReader>();
            services.TryAddSingleton<IWebhookHandler, DefaultWebhookHandler>();

            return services;
        }
    }
}
