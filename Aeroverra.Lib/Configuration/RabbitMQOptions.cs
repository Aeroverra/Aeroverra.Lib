namespace Aeroverra.Lib.Configuration
{
    /// <summary>
    /// Connection settings for the RabbitMQ broker. Bound from the <c>RabbitMQ</c> section
    /// of <c>IConfiguration</c> (typically via the <c>AddRabbitMQOptions</c> extension).
    /// Reusable across any RabbitMQ client — the bundled MassTransit setup, an external
    /// MassTransit-free publisher, an admin tool, etc.
    /// </summary>
    public class RabbitMQOptions
    {
        /// <summary>
        /// The configuration section name these options bind from.
        /// </summary>
        public const string SectionName = "RabbitMQ";

        /// <summary>
        /// RabbitMQ host name or IP (e.g. <c>"rabbitmq"</c>, <c>"100.125.17.122"</c>).
        /// </summary>
        public string HostName { get; set; } = null!;

        /// <summary>
        /// AMQP port. Defaults to the standard 5672.
        /// </summary>
        public int Port { get; set; } = 5672;

        /// <summary>
        /// Virtual host on the broker. Defaults to <c>"/"</c>.
        /// </summary>
        public string VirtualHost { get; set; } = "/";

        /// <summary>
        /// Username for AMQP authentication.
        /// </summary>
        public string Username { get; set; } = null!;

        /// <summary>
        /// Password for AMQP authentication.
        /// </summary>
        public string Password { get; set; } = null!;
    }
}
