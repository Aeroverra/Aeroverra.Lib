namespace Aeroverra.Lib.Configuration
{
    /// <summary>
    /// PayPal API credentials and partner identifiers. Bound from the <c>PayPal</c> section of
    /// <c>IConfiguration</c> (typically via the <c>AddPayPalOptions</c> extension). Reusable
    /// across any PayPal client (the official PayPalServerSDK, a custom REST wrapper, an admin
    /// tool, etc.).
    /// </summary>
    public class PayPalOptions
    {
        /// <summary>
        /// The configuration section name these options bind from.
        /// </summary>
        public const string SectionName = "PayPal";

        /// <summary>
        /// Either <c>Sandbox</c> or <c>Production</c>. Determines which PayPal endpoint the
        /// SDK / API client targets.
        /// </summary>
        public string Environment { get; set; } = "Sandbox";

        /// <summary>
        /// PayPal app client id (the public half of the client-credentials pair).
        /// </summary>
        public string ClientId { get; set; } = null!;

        /// <summary>
        /// PayPal app client secret. Pair this with <see cref="ClientId"/> when authenticating.
        /// </summary>
        public string ClientSecret { get; set; } = null!;

        /// <summary>
        /// PayPal Partner Attribution Id (BN code) — sent on every API call as the
        /// <c>PayPal-Partner-Attribution-Id</c> header so PayPal can identify integrations
        /// coming from this partner. Required for partner integrations; ignored when blank.
        /// </summary>
        public string? PartnerId { get; set; }

        /// <summary>
        /// PayPal merchant id for the partner account that owns this integration.
        /// </summary>
        public string? MerchantId { get; set; }

        /// <summary>
        /// PayPal webhook id registered for this app. Used by webhook validators to verify
        /// inbound transmissions came from PayPal.
        /// </summary>
        public string? WebhookId { get; set; }
    }
}
