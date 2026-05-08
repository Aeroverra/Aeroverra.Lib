using Aeroverra.Lib.Queues.Topology.Definitions;

namespace Aeroverra.Lib.Queues.Topology
{
    /// <summary>
    /// A mutable collection of topology definitions. Build it up in DI configuration, then
    /// hand it to <see cref="IQueueTopologyApplier"/> to declare against the broker. The
    /// definitions are passive descriptions — apply order is exchanges → queues → bindings.
    /// </summary>
    public sealed class QueueTopology
    {
        private readonly List<ExchangeDefinition> _exchanges = new();
        private readonly List<QueueDefinition> _queues = new();
        private readonly List<BindingDefinition> _bindings = new();

        public IReadOnlyList<ExchangeDefinition> Exchanges => _exchanges;
        public IReadOnlyList<QueueDefinition> Queues => _queues;
        public IReadOnlyList<BindingDefinition> Bindings => _bindings;

        /// <summary>
        /// Appends an exchange definition. Returns <c>this</c> for chaining.
        /// </summary>
        public QueueTopology AddExchange(ExchangeDefinition exchange)
        {
            _exchanges.Add(exchange);
            return this;
        }

        /// <summary>
        /// Appends a queue definition. Returns <c>this</c> for chaining.
        /// </summary>
        public QueueTopology AddQueue(QueueDefinition queue)
        {
            _queues.Add(queue);
            return this;
        }

        /// <summary>
        /// Appends a binding definition. Returns <c>this</c> for chaining.
        /// </summary>
        public QueueTopology AddBinding(BindingDefinition binding)
        {
            _bindings.Add(binding);
            return this;
        }
    }
}
