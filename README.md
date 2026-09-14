# ArcadeLink.NotificationsFunction

Azure Function (.NET 8, worker isolado) que substitui o `ArcadeLink.Notifications` (Web API da Fase
2) — mesmo comportamento observável (log de boas-vindas no cadastro, log de confirmação de compra
aprovada), agora acionado por trigger de mensageria em vez de um container sempre no ar.

## Como funciona

Duas Functions, cada uma com trigger `[RabbitMQTrigger]` numa fila própria:

- `WelcomeEmailFunction` (`Functions/WelcomeEmailFunction.cs`) — fila `function-user-created-event`,
  loga `"E-mail de boas-vindas enviado para {Email}"` quando recebe um `UserCreatedEvent`.
- `PurchaseConfirmationFunction` (`Functions/PurchaseConfirmationFunction.cs`) — fila
  `function-payment-processed-event`, loga a confirmação só quando
  `PaymentProcessedEvent.Status == "Approved"` (no-op em `Rejected` ou qualquer outro valor).

## O envelope do MassTransit

Os publishers (`ArcadeLink.Users`, `ArcadeLink.Payments`) continuam usando MassTransit, que não
publica o JSON puro do evento — embrulha a mensagem num envelope com metadados. Esta Function NÃO
usa MassTransit (não faz sentido num modelo serverless), então ela mesma desembrulha esse envelope
(`Envelope/MassTransitEnvelopeReader.cs`, `MassTransitEnvelopeReader.Unwrap<T>`) antes de
desserializar para o record do evento — o desserializador usa `PropertyNameCaseInsensitive = true`,
necessário porque o MassTransit serializa em camelCase. Um envelope malformado ou que falhe a
desserialização retorna `default`/`null` em vez de lançar — as duas Functions tratam isso como um
`LogWarning` e retornam sem processar, em vez de derrubar a execução.

## Filas: por que precisam ser criadas manualmente

Sem um consumer MassTransit rodando (como o antigo `ArcadeLink.Notifications`), nada mais cria
automaticamente a topologia de filas no RabbitMQ. O script
`../ArcadeLink.Orchestration/scripts/setup-function-queues.sh` declara as duas filas usadas aqui
(`function-user-created-event`, `function-payment-processed-event`) e faz o bind delas aos exchanges
que os publishers já usam (`ArcadeLink.Contracts:UserCreatedEvent`,
`ArcadeLink.Contracts:PaymentProcessedEvent`) via a management API do RabbitMQ — rode-o uma vez
contra o RabbitMQ (local ou do cluster) antes de testar esta Function.

## Como rodar localmente

Pré-requisitos: .NET 8 SDK, Azure Functions Core Tools v4 (`func --version`), RabbitMQ rodando.

```bash
bash ../ArcadeLink.Orchestration/scripts/setup-function-queues.sh
cp src/ArcadeLink.NotificationsFunction.Api/local.settings.json.example src/ArcadeLink.NotificationsFunction.Api/local.settings.json
cd src/ArcadeLink.NotificationsFunction.Api
func start
```

`local.settings.json` é gitignored (evita commitar segredos locais por engano, mesmo sendo só
placeholders de dev aqui) — por isso não vem versionado e precisa ser criado a partir do
`local.settings.json.example` acima num clone limpo.

**`func start` não foi validado de verdade** nas tasks anteriores. Ao scaffoldar este repositório,
o Azure Functions Core Tools não estava disponível no ambiente dos subagentes (`func --version`
retornou `command not found`, e a tentativa de instalar via `npm install -g
azure-functions-core-tools@4` não completou a tempo por rede lenta/indisponível) — ver
`../Temp/.claude/development_plans/sdd-fase3/task-4-report.md`. O scaffold foi feito manualmente
como fallback, e tudo que pôde ser validado sem o `func` (`dotnet build`, `dotnet test`) passou sem
erro, mas o comportamento real do host do Functions carregando o binding do RabbitMQ em runtime
(fora do `dotnet build`) **não foi confirmado**. Rode `func start` manualmente para validar isso
antes de considerar esta Function pronta para uma demonstração.

`local.settings.json` já aponta `RabbitMqConnection` para `amqp://guest:guest@localhost:5672` (dev).

## Testes

```bash
dotnet test
```

`tests/ArcadeLink.NotificationsFunction.Tests` (xUnit, 5 testes): `WelcomeEmailFunctionTests` e
`PurchaseConfirmationFunctionTests`, cobrindo o caminho de sucesso, o no-op em `Rejected` (só na
confirmação de compra) e o envelope malformado — sem precisar de RabbitMQ real, já que as Functions
recebem a mensagem já como `string` (o parâmetro do trigger) e são chamadas diretamente nos testes.

## Deploy

Só emulação local (`func start`), por decisão do usuário — sem deploy real no Azure.
