namespace Aeroverra.Lib.Queues.Topology.Definitions
{
    /// <summary>
    /// Declarative description of a queue. Append to a <see cref="QueueTopology"/> via
    /// <c>QueueTopology.AddQueue(...)</c> (or one of the composable extensions) to have it
    /// declared at host startup. <c>Arguments</c> carries broker-specific options like
    /// <c>x-dead-letter-exchange</c>, <c>x-message-ttl</c>, etc.
    /// </summary>
    public sealed record QueueDefinition(
        string Name,
        bool Durable = true,
        bool Exclusive = false,
        bool AutoDelete = false,
        IDictionary<string, object?>? Arguments = null);
}
