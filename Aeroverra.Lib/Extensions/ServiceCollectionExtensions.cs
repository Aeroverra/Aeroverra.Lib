using Aeroverra.Lib.Configuration;
using Aeroverra.Lib.Webhooks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aeroverra.Lib.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the default Aeroverra.Lib services (webhook reader, validator dispatcher,
        /// no-op handler). Each subsystem's docs explain what to plug in on top.
        /// </summary>
        public static IServiceCollection AddAeroverraLibServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.TryAddSingleton<IWebhookService, DefaultWebhookService>();
            services.TryAddSingleton<IWebhookReader, DefaultWebhookReader>();
            services.TryAddSingleton<IWebhookHandler, DefaultWebhookHandler>();

            return services;
        }

        /// <summary>
        /// Binds RabbitMQ broker connection settings from the <c>RabbitMQ</c> configuration
        /// section into <see cref="RabbitMQOptions"/>. Used by MassTransit (or any other
        /// RabbitMQ client) to resolve the host/credentials at bus configuration time.
        /// </summary>
        public static IServiceCollection AddRabbitMQOptions(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<RabbitMQOptions>(configuration.GetSection(RabbitMQOptions.SectionName));
            return services;
        }
    }
}
