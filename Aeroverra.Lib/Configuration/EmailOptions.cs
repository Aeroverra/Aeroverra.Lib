namespace Aeroverra.Lib.Configuration
{
    /// <summary>
    /// Outbound email credentials and display info. Bound from the <c>Email</c> section of
    /// <c>IConfiguration</c> (typically via the <c>AddEmailOptions</c> extension). Used by the
    /// queue-driven email consumer (and any other SMTP caller) to resolve host/port/password
    /// at send time.
    /// </summary>
    public class EmailOptions
    {
        /// <summary>
        /// The configuration section name these options bind from.
        /// </summary>
        public const string SectionName = "Email";

        /// <summary>
        /// SMTP host name (e.g. <c>"webmail.fr.minecraft.technology"</c>).
        /// </summary>
        public string Host { get; set; } = null!;

        /// <summary>
        /// SMTP port. No default — must be set explicitly so the wrong port doesn't silently
        /// route plaintext to a TLS-only listener.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// SMTP password used to authenticate as the sender address on each
        /// <see cref="System.Net.Mail.MailMessage"/>.
        /// </summary>
        public string Password { get; set; } = null!;

        /// <summary>
        /// Friendly From display name on outbound mail (e.g. <c>"AutoBuy"</c>). The actual
        /// sender address comes from the <c>EmailModel</c> row.
        /// </summary>
        public string DisplayName { get; set; } = null!;
    }
}
