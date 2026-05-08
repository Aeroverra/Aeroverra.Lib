namespace Aeroverra.Lib.Queues.Topology.Definitions
{
    /// <summary>
    /// Declarative description of an exchange. <see cref="Type"/> is the AMQP exchange type
    /// (<c>"fanout"</c>, <c>"direct"</c>, <c>"topic"</c>, <c>"headers"</c>) — defaults to
    /// fanout because that's the common case for dead-letter exchanges.
    /// </summary>
    public sealed record ExchangeDefinition(
        string Name,
        string Type = "fanout",
        bool Durable = true,
        bool AutoDelete = false,
        IDictionary<string, object?>? Arguments = null);
}
