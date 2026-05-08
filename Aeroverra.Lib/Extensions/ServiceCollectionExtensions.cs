using Aeroverra.Lib.Queues;
using Aeroverra.Lib.Queues.Consumers;
using Aeroverra.Lib.Queues.Topology;
using Aeroverra.Lib.Webhooks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

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

        /// <summary>
        /// Registers the typed RabbitMQ options + generic queue publisher. Anything that needs
        /// to publish to RabbitMQ (web app, worker services, etc.) should call this.
        /// Topology (queue/exchange/binding declarations) is owned separately — see
        /// <see cref="AddRabbitMQTopology"/>.
        /// </summary>
        public static IServiceCollection AddRabbitMQPublisher(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<RabbitMQOptions>(configuration.GetSection(RabbitMQOptions.SectionName));
            services.TryAddSingleton<IQueuePublisher, QueuePublisher>();
            return services;
        }

        /// <summary>
        /// Registers the queue consumer host that scales N consumers per configured queue.
        /// Call this from a dedicated processor host and register one IQueueMessageHandler per
        /// queue you want to consume. Also registers IQueuePublisher so handlers can republish
        /// messages (e.g. for app-managed retry counting with an updated body).
        /// </summary>
        public static IServiceCollection AddRabbitMQConsumerHost(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddRabbitMQPublisher(configuration);
            services.Configure<QueueProcessorOptions>(configuration.GetSection(QueueProcessorOptions.SectionName));
            services.AddHostedService<QueueConsumerHostedService>();
            return services;
        }

        /// <summary>
        /// Registers a <see cref="QueueTopology"/> singleton plus a hosted service that
        /// applies it to RabbitMQ once at host startup. Idempotent — applying the same
        /// topology twice is a no-op (RabbitMQ rejects only mismatching declarations).
        /// </summary>
        /// <example>
        /// <code>
        /// services.AddRabbitMQTopology(config, t =>
        /// {
        ///     t.AddQueueWithDeadLetter("AutoBuy.Webhooks");
        ///     t.AddSimpleQueue("AutoBuy.Notifications");
        /// });
        /// </code>
        /// </example>
        /// <remarks>
        /// Register this BEFORE <see cref="AddRabbitMQConsumerHost"/> so the topology
        /// hosted service runs first — IHostedService.StartAsync is awaited in registration
        /// order, so consumers will only bind once queues exist.
        /// </remarks>
        public static IServiceCollection AddRabbitMQTopology(
            this IServiceCollection services,
            IConfiguration configuration,
            Action<QueueTopology> configure)
        {
            services.Configure<RabbitMQOptions>(configuration.GetSection(RabbitMQOptions.SectionName));

            services.AddSingleton<QueueTopology>(_ =>
            {
                var topology = new QueueTopology();
                configure(topology);
                return topology;
            });
            services.AddSingleton<IQueueTopologyApplier, RabbitMQTopologyApplier>();
            services.AddHostedService<QueueTopologyHostedService>();

            return services;
        }
    }
}
