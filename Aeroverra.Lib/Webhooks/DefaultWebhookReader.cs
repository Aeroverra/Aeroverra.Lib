using Microsoft.AspNetCore.Http;
using System.Text;

namespace Aeroverra.Lib.Webhooks
{
    /// <inheritdoc />
    internal sealed class DefaultWebhookReader : IWebhookReader
    {
        /// <inheritdoc />
        public async Task<Webhook> ReadAsync(HttpRequest Request, CancellationToken cancellationToken)
        {
            string rawBody;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true))
            {
                rawBody = await reader.ReadToEndAsync(cancellationToken);
            }

            var headers = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in Request.Headers)
            {
                headers[header.Key] = header.Value.ToList()!;
            }

            var queryValues = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var queryParameter in Request.Query)
            {
                queryValues[queryParameter.Key] = queryParameter.Value.ToList()!;
            }

            var webhook = new Webhook
            {
                Headers = headers,
                Query = queryValues,
                TimeReceived = DateTimeOffset.UtcNow,
                RawBody = rawBody
            };

            return webhook;

        }
    }
}
