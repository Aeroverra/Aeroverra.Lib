# Webhooks

A small, provider-agnostic pipeline for receiving incoming webhooks (PayPal, Stripe, BTCPay, anything HMAC-signed). Handles the body/header read, dispatches to a per-provider signature validator, and gives you hooks to persist + enqueue work before you 200 the provider.

The lib **doesn't know any specific provider** — you bring your own validators and (optionally) your own enum of provider names.

## Contents

- [Concepts](#concepts)
- [Quick start](#quick-start)
- [Typed-enum providers (recommended)](#typed-enum-providers-recommended)
- [Customising the post-validate handler](#customising-the-post-validate-handler)
- [Multiple providers](#multiple-providers)
- [Notes & gotchas](#notes--gotchas)
- [API surface](#api-surface)

---

## Concepts

| Type | What it is | When to implement |
|---|---|---|
| `Webhook` | The captured request: `RawBody`, `Headers`, `Query`, `TimeReceived`. | Never — the lib produces it. |
| `IWebhookReader` | Reads `HttpRequest` → `Webhook`. Default implementation is fine for ~all cases. | Almost never. |
| `IWebhookValidator` | Validates one provider's signature. Must expose a stable `ProviderName`. | Once per provider. |
| `IWebhookHandler` | Post-read and post-validate hooks. Where you persist the row and enqueue work. | Once per app (replace the no-op default). |
| `IWebhookService` | Orchestrates read + dispatch + handler. Inject and call from your controller. | Never — use as-is. |

Flow on each request:

```
HttpRequest
   ↓ IWebhookReader.ReadAsync
Webhook
   ↓ IWebhookHandler.OnAfterReadAsync           (optional, runs whether validation will succeed or not)
   ↓ IWebhookValidator.ValidateAsync            (selected by provider key)
bool isValid
   ↓ IWebhookHandler.OnAfterValidateAsync       (your persist + enqueue lives here)
return (Webhook, isValid) to your controller
```

---

## Quick start

### 1. Register

```csharp
using Aeroverra.Lib.Extensions;

services.AddAeroverraLibServices(configuration);

// Replace the no-op handler with your own:
services.AddSingleton<IWebhookHandler, MyWebhookHandler>();

// Register one validator per provider you accept:
services.AddSingleton<IWebhookValidator, PayPalWebhookValidator>();
services.AddSingleton<IWebhookValidator, StripeWebhookValidator>();
```

`AddAeroverraLibServices` uses `TryAddSingleton` for the default handler/reader/service, so consumer registrations made *before* it skip the defaults; registrations made *after* it stack (last wins for `GetRequiredService`). Either pattern is fine; the former is cleaner.

### 2. Write a validator

```csharp
internal sealed class PayPalWebhookValidator(PayPalApi api, ILogger<PayPalWebhookValidator> logger)
    : IWebhookValidator
{
    public string ProviderName => "PayPal";

    public async Task<bool> ValidateAsync(Webhook webhook, CancellationToken ct = default)
    {
        var headers = webhook.Headers;
        var vModel = new VerifyWebhookVM
        {
            AuthAlgo         = headers["PAYPAL-AUTH-ALGO"][0],
            CertUrl          = headers["PAYPAL-CERT-URL"][0],
            TransmissionId   = headers["PAYPAL-TRANSMISSION-ID"][0],
            TransmissionSig  = headers["PAYPAL-TRANSMISSION-SIG"][0],
            TransmissionTime = headers["PAYPAL-TRANSMISSION-TIME"][0],
            WebhookEvent     = JObject.Parse(webhook.RawBody),
            WebhookId        = api.WebhookId,
        };
        return await api.VerifyWebhookAsync(vModel, ct);
    }
}
```

### 3. Write a handler

This is where durable persistence lives. Save + enqueue **before** returning 200 to the provider, so a process crash doesn't drop the event.

```csharp
public sealed class MyWebhookHandler(IDbContextFactory<AppDb> dbFactory)
    : IWebhookHandler
{
    public Task OnAfterReadAsync(Webhook webhook, CancellationToken ct) => Task.CompletedTask;

    public async Task OnAfterValidateAsync(string provider, bool isValid, Webhook webhook, CancellationToken ct)
    {
        using var dbContext = dbFactory.CreateDbContext();
        var row = new WebhookReceived
        {
            Provider                       = provider,
            RawBody                        = webhook.RawBody,
            Headers                        = webhook.Headers,
            Query                          = webhook.Query,
            ProviderVerificationSuccessful = isValid,
            TimeReceived                   = webhook.TimeReceived,
        };
        dbContext.WebhookReceived.Add(row);
        await dbContext.SaveChangesAsync(ct);
        // Then enqueue downstream processing — your queue framework of choice.
        // (e.g. MassTransit: await bus.GetSendEndpoint(...).Send(new MyMessage(row.Id)).)
    }
}
```

### 4. Call it from your controller

```csharp
[HttpPost]
[AllowAnonymous]
public async Task<IActionResult> PayPal(CancellationToken ct)
{
    var (webhook, isValid) = await _webhookService.ReadAndValidateAsync("PayPal", HttpContext.Request, ct);
    return isValid ? Ok() : Unauthorized();
}
```

---

## Typed-enum providers (recommended)

Stringly-typed `"PayPal"` keys are typo-prone. If your set of providers is closed (most apps), use `WebhookValidator<TEnum>` plus the typed extension overloads.

### Define your enum

```csharp
public enum WebhookProvider { PayPal, Stripe, BTCPay }
```

### Derive your validator from `WebhookValidator<TEnum>`

```csharp
internal sealed class PayPalWebhookValidator(PayPalApi api)
    : WebhookValidator<WebhookProvider>
{
    public override WebhookProvider Provider => WebhookProvider.PayPal;

    public override async Task<bool> ValidateAsync(Webhook webhook, CancellationToken ct = default)
    {
        // ...
    }
}
```

### Call with the enum value, not a string

```csharp
var (webhook, isValid) = await _webhookService.ReadAndValidateAsync(
    WebhookProvider.PayPal, HttpContext.Request, ct);
```

The lib never sees your enum — under the hood it `.ToString()`s it back to a string. Two consequences:

- The string-keyed core API still works; the enum overlay is purely additive.
- Two consumers can use two different enums in the same process without colliding (the dispatch map is keyed by string).

If your set of providers is **runtime-extensible** (plugins, tenant-supplied webhooks), skip the enum overlay and stick with the string API.

---

## Customising the post-validate handler

`IWebhookHandler` is the right place for app-specific behaviour. The default implementation (`DefaultWebhookHandler`) is a no-op so the service can run without one configured.

Two important properties of `OnAfterValidateAsync`:

1. It runs on the request path, before the response is sent. That is **intentional** — durable webhook handling requires persistence to complete before the provider sees a 200, otherwise a process crash silently drops the event.
2. Throwing from it propagates out of `ReadAndValidateAsync` to your controller. ASP.NET will return 500, and the provider will retry. If you want in-process retry first to avoid the slow round trip, wrap the body in [Polly](https://github.com/App-vNext/Polly):

```csharp
private static readonly ResiliencePipeline _retry = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 4,
        Delay = TimeSpan.FromMilliseconds(200),
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        ShouldHandle = new PredicateBuilder().Handle<Exception>(ex =>
            ex is DbUpdateException && ex is not DbUpdateConcurrencyException),
    })
    .Build();

public Task OnAfterValidateAsync(string provider, bool isValid, Webhook webhook, CancellationToken ct)
    => _retry.ExecuteAsync(async token => { /* persist + enqueue */ }, ct).AsTask();
```

For idempotency on provider retries, derive a stable id from the payload (PayPal's `id`, Stripe's `id`, etc.), persist with a unique index, and treat unique-violation as success.

---

## Multiple providers

Register one `IWebhookValidator` per provider:

```csharp
services.AddSingleton<IWebhookValidator, PayPalWebhookValidator>();
services.AddSingleton<IWebhookValidator, StripeWebhookValidator>();
services.AddSingleton<IWebhookValidator, BTCPayWebhookValidator>();
```

Dispatch is by `ProviderName` (case-sensitive string match). One controller endpoint per provider:

```csharp
[HttpPost("paypal")] public Task<IActionResult> PayPal(CancellationToken ct)
    => HandleAsync(WebhookProvider.PayPal, ct);

[HttpPost("stripe")] public Task<IActionResult> Stripe(CancellationToken ct)
    => HandleAsync(WebhookProvider.Stripe, ct);

private async Task<IActionResult> HandleAsync(WebhookProvider provider, CancellationToken ct)
{
    var (_, isValid) = await _webhookService.ReadAndValidateAsync(provider, HttpContext.Request, ct);
    return isValid ? Ok() : Unauthorized();
}
```

If you want a single endpoint per `{provider}` route token, just bind the route value and call the typed overload — but watch out for unknown values; the dispatcher throws `KeyNotFoundException` for unregistered providers.

---

## Notes & gotchas

- **Body is read once.** `DefaultWebhookReader` consumes `HttpRequest.Body` to EOF. If any code downstream of the service tries to read `Request.Body` again, it will get an empty string. Use `webhook.RawBody` (returned in the tuple) instead. If you need ASP.NET's `Request.Body` to remain readable for other middleware, call `Request.EnableBuffering()` before invoking the service.
- **Header lookup is case-insensitive.** The default reader stores headers in a `Dictionary<string, List<string>>` keyed `OrdinalIgnoreCase`. Don't rely on canonical casing.
- **Validator exceptions are swallowed.** If your `ValidateAsync` throws, `DefaultWebhookService` logs the exception and treats the webhook as invalid. Fail fast inside the validator only when you genuinely mean "reject."
- **Duplicate provider registrations.** Last registration wins; a `LogTrace` is emitted on collision.
- **Unknown providers.** Calling `ReadAndValidateAsync("Coinbase", …)` when no `IWebhookValidator` has `ProviderName == "Coinbase"` throws `KeyNotFoundException` from the dispatch map (caught and logged, returns `false` to the caller).
- **`OnAfterValidateAsync` runs even on validation failure.** This is deliberate — persist invalid attempts (with `isValid = false`) so you have an audit trail. If you want to drop them, branch inside the handler.

---

## API surface

```csharp
namespace Aeroverra.Lib.Webhooks;

public sealed class Webhook
{
    public required string RawBody { get; set; }
    public required Dictionary<string, List<string>> Headers { get; init; }
    public required Dictionary<string, List<string>> Query { get; init; }
    public required DateTimeOffset TimeReceived { get; init; }
}

public interface IWebhookService
{
    Task<(Webhook webhook, bool isValid)> ReadAndValidateAsync(
        string provider, HttpRequest request, CancellationToken cancellationToken = default);
    Task<Webhook> ReadAsync(HttpRequest request, CancellationToken cancellationToken = default);
    Task<bool> ValidateAsync(string provider, Webhook webhook, CancellationToken cancellationToken = default);
}

public interface IWebhookReader
{
    Task<Webhook> ReadAsync(HttpRequest request, CancellationToken cancellationToken);
}

public interface IWebhookValidator
{
    string ProviderName { get; }
    Task<bool> ValidateAsync(Webhook webhook, CancellationToken cancellationToken = default);
}

public abstract class WebhookValidator<TEnum> : IWebhookValidator
    where TEnum : struct, Enum
{
    public abstract TEnum Provider { get; }
    public string ProviderName => Provider.ToString();
    public abstract Task<bool> ValidateAsync(Webhook webhook, CancellationToken cancellationToken = default);
}

public interface IWebhookHandler
{
    Task OnAfterReadAsync(Webhook webhook, CancellationToken cancellationToken);
    Task OnAfterValidateAsync(string provider, bool isValid, Webhook webhook, CancellationToken cancellationToken);
}

public static class WebhookServiceExtensions
{
    public static Task<(Webhook, bool)> ReadAndValidateAsync<TEnum>(
        this IWebhookService service, TEnum provider, HttpRequest request,
        CancellationToken cancellationToken = default) where TEnum : struct, Enum;

    public static Task<bool> ValidateAsync<TEnum>(
        this IWebhookService service, TEnum provider, Webhook webhook,
        CancellationToken cancellationToken = default) where TEnum : struct, Enum;
}
```
