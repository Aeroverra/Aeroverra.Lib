using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Aeroverra.Lib.Queues
{
    /// <summary>
    /// Internal helpers shared by the publisher, consumer host, and topology applier so
    /// connection-factory wiring and startup-retry behavior stays consistent across all
    /// three. Kept internal — consumers of the library shouldn't need to call directly.
    /// </summary>
    internal static class RabbitMQConnectionHelpers
    {
        /// <summary>
        /// Default delay between connection attempts. Matches RabbitMQ.Client's default
        /// <c>NetworkRecoveryInterval</c> so startup retries and post-startup auto-recovery
        /// feel the same operationally.
        /// </summary>
        public static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Builds a <see cref="ConnectionFactory"/> from the bound <see cref="RabbitMQOptions"/>.
        /// Pure mapping — no IO.
        /// </summary>
        public static ConnectionFactory CreateFactory(RabbitMQOptions rabbit) => new()
        {
            HostName = rabbit.HostName,
            Port = rabbit.Port,
            VirtualHost = rabbit.VirtualHost,
            UserName = rabbit.Username,
            Password = rabbit.Password,
        };

        /// <summary>
        /// Connects with infinite retries at <see cref="DefaultRetryDelay"/> intervals.
        /// Lets a worker boot before the broker is reachable (k8s pod ordering, broker
        /// warm-up, etc.) and only bails when the supplied cancellation token fires.
        /// </summary>
        public static async Task<IConnection> ConnectWithRetryAsync(
            ConnectionFactory factory,
            ILogger logger,
            CancellationToken cancellationToken,
            TimeSpan? retryDelay = null)
        {
            var delay = retryDelay ?? DefaultRetryDelay;
            var attempt = 0;
            while (true)
            {
                attempt++;
                try
                {
                    logger.LogInformation("Connecting to RabbitMQ at {Host}:{Port}/{VHost} (attempt {Attempt})",
                        factory.HostName, factory.Port, factory.VirtualHost, attempt);
                    return await factory.CreateConnectionAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Failed to connect to RabbitMQ at {Host}:{Port}/{VHost} (attempt {Attempt}). Retrying in {Delay}s.",
                        factory.HostName, factory.Port, factory.VirtualHost, attempt, delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }
    }
}
