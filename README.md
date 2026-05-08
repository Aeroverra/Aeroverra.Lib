# Aeroverra.Lib

Small set of cross-cutting utilities used across Aeroverra projects.

The pieces shipped today:

| Area | What it does | Docs |
|---|---|---|
| Webhooks | Provider-agnostic read + validate pipeline for incoming webhooks (PayPal, Stripe, BTCPay, …). Provides interfaces, defaults, DI wiring, and an optional typed-enum overlay. | [docs/Webhooks.md](docs/Webhooks.md) |

## Install

Reference `Aeroverra.Lib.csproj` from your project, or pack and consume locally.

```xml
<ItemGroup>
  <ProjectReference Include="..\Aeroverra.Lib\Aeroverra.Lib\Aeroverra.Lib.csproj" />
</ItemGroup>
```

Targets `net10.0` with `ImplicitUsings` and `Nullable` enabled.

## Wire up

```csharp
using Aeroverra.Lib.Extensions;

services.AddAeroverraLibServices(configuration);
```

That registers the default services as singletons. Each subsystem documents what to plug in on top — e.g. webhook validators, a custom `IWebhookHandler`. See the area-specific docs.

## License

MIT — see [LICENSE.md](LICENSE.md).
