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

        /// <summary>
        /// Binds PayPal API credentials and partner identifiers from the <c>PayPal</c>
        /// configuration section into <see cref="PayPalOptions"/>. Used by the PayPalServerSDK
        /// client (or any other PayPal caller) to resolve auth + partner attribution at
        /// startup time.
        /// </summary>
        public static IServiceCollection AddPayPalOptions(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<PayPalOptions>(configuration.GetSection(PayPalOptions.SectionName));
            return services;
        }

        /// <summary>
        /// Binds outbound email credentials from the <c>Email</c> configuration section into
        /// <see cref="EmailOptions"/>. Used by the queue-driven email sender (and any other
        /// SMTP caller) to resolve host/port/password at send time.
        /// </summary>
        public static IServiceCollection AddEmailOptions(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
            return services;
        }
    }
}
