# Queues

A small, opinionated framework on top of [RabbitMQ.Client](https://www.nuget.org/packages/RabbitMQ.Client/) for publishing and consuming messages. Three pieces, each registered independently:

- **Topology** — declarative description of queues, exchanges, and bindings, applied once at host startup.
- **Publisher** — wraps payloads in an `Envelope<T>` and publishes; one long-lived connection per process.
- **Consumer host** — opens a connection, scales N consumers per queue, dispatches each delivery to a typed `QueueMessageHandler<T>`.

All three share a single `RabbitMQ` config section and use the library's connect-with-retry helper, so a worker can start before the broker is reachable.

## Contents

- [Concepts](#concepts)
- [Quick start](#quick-start)
- [Configuration](#configuration)
- [Publishing](#publishing)
- [Consuming](#consuming)
- [Outcomes & retries](#outcomes--retries)
- [Topology](#topology)
- [Scaling](#scaling)
- [Error handling & resilience](#error-handling--resilience)
- [Notes & gotchas](#notes--gotchas)
- [API surface](#api-surface)

---

## Concepts

| Type | What it is | When to use |
|---|---|---|
| `RabbitMQOptions` | Connection settings (host, port, vhost, credentials). Bound from config. | Once per app, via DI registration. |
| `IQueuePublisher` | Sends messages. Standard path wraps in `Envelope<T>`; raw path doesn't. | Inject anywhere you need to publish. |
| `Envelope<T>` | Wrapper carrying `Id`, `AttemptCount`, `TimePublished`, and a typed `Payload`. | The default message shape — producer wraps, consumer unwraps automatically. |
| `IQueueMessageHandler` | Low-level consumer contract. Receives the raw body string. | Only for non-envelope flows. |
| `QueueMessageHandler<T>` | Typed handler base. Parses the envelope, enforces the retry cap, handles republish-on-Retry. | The default — extend this for every queue you consume. |
| `QueueHandlerResult` | Outcome returned by handlers: `Success` / `Fail(requeue, reason)` / `Retry(reason)`. | Returned from every handler invocation. |
| `QueueTopology` | A mutable collection of `QueueDefinition`, `ExchangeDefinition`, `BindingDefinition`. | Built up at DI registration time. |
| `IQueueTopologyApplier` | Applies a topology to the broker. RabbitMQ implementation declares everything in order. | Don't inject directly — the hosted service runs it at startup. |
| `QueueConsumerHostedService` | Background service that opens the connection and starts N consumers per queue. | Registered via `AddRabbitMQConsumerHost`. |
| `QueueProcessorOptions` | Per-queue consumer settings (`ConsumerCount`, `PrefetchCount`). Bound from config. | Configures how the consumer host scales. |

**Flow on each delivery:**

```
broker delivery
   ↓ AsyncEventingBasicConsumer.ReceivedAsync
QueueConsumerHostedService.HandleAsync
   ↓ resolves IQueueMessageHandler (by QueueName) inside a fresh DI scope
QueueMessageHandler<T>.HandleAsync (interface impl)
   ↓ JsonConvert.DeserializeObject<Envelope<T>>(body)
   ↓ checks AttemptCount < MaxAttempts
   ↓ dispatches to your protected HandleAsync(Envelope<T>, ...)
QueueHandlerResult result
   ↓ if Retry: republish envelope with AttemptCount+1, return Success() (host acks original)
   ↓ if Success: host acks
   ↓ if Fail: host nacks (requeue per result)
```

---

## Quick start

### 1. Configure

`appsettings.json`:

```json
{
  "RabbitMQ": {
    "HostName": "rabbitmq",
    "Port": 5672,
    "VirtualHost": "Production",
    "Username": "myapp",
    "Password": "..."
  },
  "QueueProcessor": {
    "Queues": {
      "MyApp.Webhooks": {
        "ConsumerCount": 3,
        "PrefetchCount": 10
      }
    }
  }
}
```

### 2. Register in your worker host

```csharp
using Aeroverra.Lib.Extensions;
using Aeroverra.Lib.Queues.Consumers;
using Aeroverra.Lib.Queues.Topology;

var builder = Host.CreateApplicationBuilder(args);

// Topology — runs first, declares queues/exchanges/bindings
builder.Services.AddRabbitMQTopology(builder.Configuration, topology =>
{
    topology.AddQueueWithDeadLetter("MyApp.Webhooks");
});

// Consumer host — also registers IQueuePublisher
builder.Services.AddRabbitMQConsumerHost(builder.Configuration);

// One handler per queue
builder.Services.AddScoped<IQueueMessageHandler, MyWebhookHandler>();

await builder.Build().RunAsync();
```

### 3. Register in a publish-only host (web app, etc.)

```csharp
using Aeroverra.Lib.Extensions;

services.AddRabbitMQPublisher(configuration);
```

If your web app is the source of truth for topology, also call `AddRabbitMQTopology` there. If the worker owns topology, the web app can skip it — publishing to an undeclared queue with `mandatory: true` will fail loudly so misconfigurations show up.

### 4. Write a handler

```csharp
using Aeroverra.Lib.Queues;
using Aeroverra.Lib.Queues.Consumers;
using RabbitMQ.Client.Events;

public class MyWebhookHandler(IQueuePublisher publisher, ILogger<MyWebhookHandler> logger)
    : QueueMessageHandler<Guid>(publisher, logger)
{
    public override string QueueName => "MyApp.Webhooks";
    public override int MaxAttempts => 5;

    protected override async Task<QueueHandlerResult> HandleAsync(
        Envelope<Guid> envelope,
        BasicDeliverEventArgs deliverArgs,
        CancellationToken ct)
    {
        var webhookId = envelope.Payload;
        // … real work …
        return QueueHandlerResult.Success();
    }
}
```

### 5. Publish

```csharp
public class WebhookController(IQueuePublisher publisher) : Controller
{
    [HttpPost]
    public async Task<IActionResult> Receive(WebhookPayload body)
    {
        var id = Guid.NewGuid();
        await SaveToDb(id, body);                           // persist first
        await publisher.PublishAsync("MyApp.Webhooks", id); // then enqueue
        return Ok();
    }
}
```

The publisher generates an `Envelope<Guid>` with `Id` (auto-generated GUID if not supplied), `AttemptCount = 0`, `TimePublished = now`, and `Payload = id`.

---

## Configuration

### `RabbitMQ` section → `RabbitMQOptions`

| Property | Default | Notes |
|---|---|---|
| `HostName` | required | Broker hostname or IP. |
| `Port` | `5672` | AMQP port. |
| `VirtualHost` | `"/"` | Vhost on the broker. |
| `Username` | required | AMQP user. |
| `Password` | required | AMQP password. |

### `QueueProcessor` section → `QueueProcessorOptions`

```json
"QueueProcessor": {
  "Queues": {
    "MyApp.Webhooks":      { "ConsumerCount": 3, "PrefetchCount": 10 },
    "MyApp.Notifications": { "ConsumerCount": 1, "PrefetchCount": 5 }
  }
}
```

| Property | Default | Notes |
|---|---|---|
| `Queues` | empty | Dictionary keyed by queue name. A queue absent here is not consumed (even if a handler is registered). |
| `Queues[queue].ConsumerCount` | `1` | Number of independent channels with their own consumers. Set to `0` to disable a queue without removing config. |
| `Queues[queue].PrefetchCount` | `10` | Per-consumer unacked-message ceiling. See [Scaling](#scaling). |

---

## Publishing

### Standard path: `PublishAsync<T>(queueName, payload, id?)`

Wraps `payload` in an `Envelope<T>`, generates a GUID id if not supplied, and publishes:

```csharp
await publisher.PublishAsync("MyApp.Webhooks", webhookGuid);
await publisher.PublishAsync("MyApp.Orders", orderDto, id: orderId.ToString());
```

### Raw path: `PublishRawAsync<TMessage>(queueName, id, message)`

Publishes the message exactly as given — no envelope wrapping. Use when integrating with non-envelope producers/consumers, or when republishing an already-built envelope verbatim.

```csharp
await publisher.PublishRawAsync("LegacyQueue", correlationId, rawDto);
```

### Project-specific extensions

Wrap publish calls in extension methods on `IQueuePublisher` so call sites stay declarative and the queue-name + payload mapping lives in one place:

```csharp
public static class MyAppQueuePublisherExtensions
{
    extension(IQueuePublisher publisher)
    {
        public Task PublishWebhookAsync(WebhookReceived row)
            => publisher.PublishAsync("MyApp.Webhooks", row.Id, id: $"{row.Id}");
    }
}

// call site
await publisher.PublishWebhookAsync(row);
```

---

## Consuming

### Typed: `QueueMessageHandler<T>` (default)

Extend the base class. Override `QueueName`, optionally `MaxAttempts`, and the protected `HandleAsync`:

```csharp
public class OrderShippedHandler(IQueuePublisher publisher, ILogger<OrderShippedHandler> logger)
    : QueueMessageHandler<OrderShippedEvent>(publisher, logger)
{
    public override string QueueName => "MyApp.OrderShipped";
    public override int MaxAttempts => 3;

    protected override async Task<QueueHandlerResult> HandleAsync(
        Envelope<OrderShippedEvent> envelope,
        BasicDeliverEventArgs deliverArgs,
        CancellationToken ct)
    {
        var evt = envelope.Payload;
        // …
        return QueueHandlerResult.Success();
    }
}
```

The base owns: JSON parsing, attempt-cap enforcement, republish-on-Retry. Your code is pure business logic.

### Raw: `IQueueMessageHandler` (escape hatch)

Implement the bare interface when consuming bodies that aren't `Envelope<T>`. The host calls you with the raw UTF-8 body. No retry support — the host treats `Retry` outcomes from raw handlers as `Fail(requeue: false)` because there's no envelope to update.

```csharp
public class RawCsvIngestHandler(ILogger<RawCsvIngestHandler> log) : IQueueMessageHandler
{
    public string QueueName => "MyApp.LegacyCsvDrops";

    public Task<QueueHandlerResult> HandleAsync(
        string body, BasicDeliverEventArgs ea, CancellationToken ct)
    {
        // parse body yourself
        return Task.FromResult(QueueHandlerResult.Success());
    }
}
```

### Registration

Register handlers as **scoped** so each delivery gets a fresh DI scope (clean DbContext lifetime, etc.):

```csharp
services.AddScoped<IQueueMessageHandler, OrderShippedHandler>();
services.AddScoped<IQueueMessageHandler, MyWebhookHandler>();
```

The host resolves `GetServices<IQueueMessageHandler>()` and routes by matching `QueueName`. Don't register two handlers with the same `QueueName` — first match wins.

---

## Outcomes & retries

Every handler returns a `QueueHandlerResult`. There are three outcomes:

### `Success()`

Handler completed. Host acks the delivery.

### `Fail(requeue, reason)`

Handler rejected the message.

- `requeue: true` — broker-level requeue. Instant redelivery. **No attempt tracking — can loop forever.** Use sparingly, e.g. when you want broker-handled "try again immediately."
- `requeue: false` — broker drops the message, or routes to the DLX if one is configured. Use for poison messages (bad data, schema mismatch) or after exhausting your own retry budget.

### `Retry(reason)`

Handler signals a transient failure with attempt tracking. The typed base republishes the envelope with `AttemptCount + 1` and returns `Success()` to the host (ack original). The new copy hits this same handler later; if it keeps failing, the `MaxAttempts` cap eventually trips and short-circuits to `Fail(requeue: false)`.

This is the **right path for app-managed retries** — capped, durable, observable in logs.

### `MaxAttempts`

The cap fires *before* invoking your handler when an arriving envelope already has `AttemptCount >= MaxAttempts`. The base returns `Fail(requeue: false, reason: "Max attempts (N) exceeded")` and the broker drops or DLX-routes. With a DLX configured, dead-lettered messages stay queryable for inspection or replay.

### Uncaught exceptions

Handler throws, the host's outer catch turns it into a nack-without-requeue (poison-pill protection). Logged at `Error` with the stack trace. The host process stays alive.

---

## Topology

Queue/exchange/binding declarations are declarative and applied once at startup, not lazily on first publish. Publisher and consumer host both **trust** that topology already exists.

### The pattern

```csharp
services.AddRabbitMQTopology(configuration, topology =>
{
    topology.AddQueueWithDeadLetter("MyApp.Webhooks");
    topology.AddSimpleQueue("MyApp.Notifications");

    // Or build it up by hand:
    topology.AddExchange(new ExchangeDefinition("audit-events", ExchangeType.Topic));
    topology.AddQueue(new QueueDefinition("audit.high"));
    topology.AddBinding(new BindingDefinition(
        Source: "audit-events",
        Destination: "audit.high",
        RoutingKey: "audit.severity.high"));
});
```

The hosted service runs `IQueueTopologyApplier.ApplyAsync` during host startup, in the order: exchanges → queues → bindings. Idempotent — re-applying the same topology is a no-op (RabbitMQ rejects only mismatching declarations).

### Composable patterns

`QueueTopologyExtensions` exposes shortcuts that just append definitions:

| Method | What it adds |
|---|---|
| `AddSimpleQueue(name)` | A plain durable queue. |
| `AddQueueWithDeadLetter(name, dlx?, dlq?)` | A fanout DLX, a DLQ bound to it, and the main queue with `x-dead-letter-exchange` set. Defaults: `<name>.dlx`, `<name>.deadletter`. |

Add new patterns as you need them — TTL retry queues, priority queues, lazy queues, quorum queues, etc.

### Definitions

`QueueDefinition`, `ExchangeDefinition`, `BindingDefinition` are immutable records. `Arguments` carries broker-specific options (`x-dead-letter-exchange`, `x-message-ttl`, `x-max-length`, `x-queue-mode`, etc.).

```csharp
new QueueDefinition(
    Name: "MyApp.Slow",
    Arguments: new Dictionary<string, object?>
    {
        ["x-message-ttl"] = 86_400_000, // 24h
        ["x-max-length"]  = 10_000,
    });
```

### Why no declarations in publisher / consumer

A single source of truth for queue args avoids the `PRECONDITION_FAILED` mismatch errors you get when one place declares a queue without args and another declares it with `x-dead-letter-exchange`. Topology is owned by config, applied at startup, and everywhere else trusts it.

---

## Scaling

### Within a process

`ConsumerCount` per queue creates that many independent channels, each with its own consumer. Increase to parallelize handler work within one worker.

`PrefetchCount` per consumer caps unacked messages held by that consumer at any time. Picking a value:

- **Low (1–5)** — fair distribution, good for slow handlers (network-bound, multi-second). Each ack costs a round-trip, so latency limits throughput.
- **Medium (10–50)** — most app handlers (DB + outbound HTTP). Default.
- **High (100+)** — fast in-memory handlers. Beware head-of-line blocking and memory pressure.

Rough heuristic: `prefetch ≈ broker_rtt_ms / handler_avg_ms`. If a message takes 100ms and broker RTT is 1ms, prefetch ≈ 100 keeps the consumer saturated without hoarding.

### Across processes

Run multiple worker instances. Each opens its own connection and registers its own consumers. RabbitMQ round-robins deliveries across all consumers on a queue (subject to per-consumer prefetch). No coordination needed.

---

## Error handling & resilience

### Auto-recovery

`AutomaticRecoveryEnabled` and `TopologyRecoveryEnabled` are on by default in RabbitMQ.Client v7. On a connection drop the library reconnects every `NetworkRecoveryInterval` (5s default), re-declares topology, and re-registers consumers. The `IConnection` and `IChannel` references stay valid across recovery.

In-flight handlers continue running after a disconnect. Their eventual ack/nack throws `AlreadyClosedException`, which the host catches and logs at `Warning`. The broker has already redelivered the unacked message — handlers must be idempotent.

### Startup connect retry

`RabbitMQConnectionHelpers.ConnectWithRetryAsync` retries forever (every 5s) until either the broker is reachable or the host's `stoppingToken` fires. The publisher, consumer host, and topology applier all use this, so a worker can boot before the broker is up (k8s pod ordering, broker warm-up) without crashing.

### Connection / channel shutdown logging

The consumer host subscribes to `IConnection.ConnectionShutdownAsync` and per-channel `ChannelShutdownAsync` for visibility:

```
warn: RabbitMQ connection shutdown — initiator=Library code=541 text=...
warn: [worker-MyApp.Webhooks-0] Channel shutdown — initiator=... Auto-recovery will re-register the consumer.
```

Logs only — the events don't trigger any custom recovery; the library handles that.

### Handler exceptions

Caught by the host's outer `catch (Exception)`. Logged at `Error`, message nacked without requeue (poison-pill protection), host stays alive. Other consumers / messages on the same channel keep flowing.

`AlreadyClosedException` on ack/nack is special-cased to `Warning` — the broker has already redelivered, handlers must be idempotent.

---

## Notes & gotchas

- **At-least-once delivery.** Even on the happy path, network blips and consumer crashes cause occasional duplicates. Design handlers to be idempotent (unique constraints, idempotency keys, etc.).
- **The `Retry` outcome only works in `QueueMessageHandler<T>`.** Raw `IQueueMessageHandler` implementations returning `Retry` get treated as `Fail(requeue: false)` because the host has no envelope to update.
- **`Fail(requeue: true)` has no attempt cap** — it can loop forever on a permanently-broken message. Prefer `Retry()` for transient failures.
- **`AttemptCount` is preserved on republish, but `TimePublished` is too** — so end-to-end SLA monitoring stays accurate even after retries.
- **No DLX = silent drop.** With no DLX configured, `Fail(requeue: false)` deletes the message. Configure a DLX (via `AddQueueWithDeadLetter`) if you want dropped messages to be inspectable.
- **Topology mismatches** raise `PRECONDITION_FAILED`. Most common cause: queue declared once without DLX args, then later with them. Fix: delete the queue once via the management UI, then redeploy.
- **Handlers are scoped.** Each delivery resolves a fresh handler instance from a child DI scope, so DbContext / scoped services follow normal request-style lifetimes.
- **Same-process consumer counter sharing.** A `static` field used by demo handlers is shared across all consumers in the process — not a problem for production code that uses `envelope.AttemptCount` for state.

---

## API surface

```csharp
namespace Aeroverra.Lib.Queues;

public sealed record Envelope<T>
{
    public string Id { get; init; }
    public int AttemptCount { get; init; }
    public DateTimeOffset TimePublished { get; init; }
    public T Payload { get; init; }
}

public interface IQueuePublisher
{
    Task PublishAsync<T>(string queueName, T payload, string? id = null);
    Task PublishRawAsync<TMessage>(string queueName, string id, TMessage message);
}

public class RabbitMQOptions
{
    public const string SectionName = "RabbitMQ";
    public string HostName { get; set; }
    public int Port { get; set; }            // default 5672
    public string VirtualHost { get; set; }  // default "/"
    public string Username { get; set; }
    public string Password { get; set; }
}
```

```csharp
namespace Aeroverra.Lib.Queues.Consumers;

public interface IQueueMessageHandler
{
    string QueueName { get; }
    Task<QueueHandlerResult> HandleAsync(string body, BasicDeliverEventArgs deliverArgs, CancellationToken cancellationToken);
}

public abstract class QueueMessageHandler<T> : IQueueMessageHandler
{
    protected QueueMessageHandler(IQueuePublisher publisher, ILogger logger);
    public abstract string QueueName { get; }
    public virtual int MaxAttempts { get; } = 5;
    protected abstract Task<QueueHandlerResult> HandleAsync(
        Envelope<T> envelope,
        BasicDeliverEventArgs deliverArgs,
        CancellationToken cancellationToken);
}

public enum QueueHandlerOutcome { Success, Fail, Retry }

public readonly record struct QueueHandlerResult
{
    public QueueHandlerOutcome Outcome { get; }
    public bool Requeue { get; }
    public string? Reason { get; }
    public bool IsSuccess { get; }
    public bool IsFail { get; }
    public bool IsRetry { get; }

    public static QueueHandlerResult Success();
    public static QueueHandlerResult Fail(bool requeue, string reason);
    public static QueueHandlerResult Retry(string reason);
}

public class QueueProcessorOptions
{
    public const string SectionName = "QueueProcessor";
    public Dictionary<string, QueueConsumerOptions> Queues { get; set; }
}

public class QueueConsumerOptions
{
    public int ConsumerCount { get; set; }    // default 1
    public ushort PrefetchCount { get; set; } // default 10
}
```

```csharp
namespace Aeroverra.Lib.Queues.Topology;

public sealed class QueueTopology
{
    public IReadOnlyList<ExchangeDefinition> Exchanges { get; }
    public IReadOnlyList<QueueDefinition> Queues { get; }
    public IReadOnlyList<BindingDefinition> Bindings { get; }

    public QueueTopology AddExchange(ExchangeDefinition exchange);
    public QueueTopology AddQueue(QueueDefinition queue);
    public QueueTopology AddBinding(BindingDefinition binding);
}

public interface IQueueTopologyApplier
{
    Task ApplyAsync(QueueTopology topology, CancellationToken cancellationToken = default);
}

public static class QueueTopologyExtensions
{
    public static QueueTopology AddSimpleQueue(this QueueTopology t, string name);
    public static QueueTopology AddQueueWithDeadLetter(
        this QueueTopology t, string queueName,
        string? deadLetterExchange = null, string? deadLetterQueue = null);
}
```

```csharp
namespace Aeroverra.Lib.Queues.Topology.Definitions;

public sealed record QueueDefinition(
    string Name,
    bool Durable = true,
    bool Exclusive = false,
    bool AutoDelete = false,
    IDictionary<string, object?>? Arguments = null);

public sealed record ExchangeDefinition(
    string Name,
    string Type = "fanout",
    bool Durable = true,
    bool AutoDelete = false,
    IDictionary<string, object?>? Arguments = null);

public sealed record BindingDefinition(
    string Source,
    string Destination,
    string RoutingKey = "",
    IDictionary<string, object?>? Arguments = null);
```

```csharp
namespace Aeroverra.Lib.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMQPublisher(
        this IServiceCollection services, IConfiguration configuration);

    public static IServiceCollection AddRabbitMQConsumerHost(
        this IServiceCollection services, IConfiguration configuration);

    public static IServiceCollection AddRabbitMQTopology(
        this IServiceCollection services, IConfiguration configuration,
        Action<QueueTopology> configure);
}
```
