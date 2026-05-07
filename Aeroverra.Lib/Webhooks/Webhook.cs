namespace Aeroverra.Lib.Webhooks
{
    /// <summary>
    /// Represents a webhook request received by the application and handled by the <see cref="IWebhookService"/>
    /// </summary>
    public sealed class Webhook
    {
        /// <summary>
        /// Raw body of the webhook
        /// </summary>
        public required string RawBody { get; init; }

        /// <summary>
        /// Request Headers
        /// </summary>
        public required Dictionary<string, List<string>> Headers { get; init; }

        /// <summary>
        /// Query string values
        /// </summary>
        public required Dictionary<string, List<string>> Query { get; init; }

        /// <summary>
        /// The time the webhook was received by the application
        /// </summary>
        public required DateTimeOffset TimeReceived { get; init; } = DateTimeOffset.UtcNow;
    }
}
