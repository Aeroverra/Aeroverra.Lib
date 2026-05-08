namespace Aeroverra.Lib.Queues.Topology.Definitions
{
    /// <summary>
    /// Queue-to-exchange binding. <see cref="Source"/> is the exchange name, <see cref="Destination"/>
    /// is the queue name. Routing key is fanout-irrelevant but used by direct/topic exchanges.
    /// </summary>
    public sealed record BindingDefinition(
        string Source,
        string Destination,
        string RoutingKey = "",
        IDictionary<string, object?>? Arguments = null);
}
