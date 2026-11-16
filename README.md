# ArcadeLink.NotificationsFunction

Notificações do ArcadeLink fora do caminho principal. O repositório tem **duas** implementações do mesmo
comportamento observável (log de boas-vindas no cadastro, log de confirmação de compra aprovada):

| Implementação | Fase | Status |
|---|---|---|
| `src/ArcadeLink.NotificationsLambda` + `terraform/` — **AWS Lambda** (.NET 8) acionada por SQS | 4 | **Opcional**: só existe se você ligar o transporte `AmazonSqs` |
| `src/ArcadeLink.NotificationsFunction.Api` — **Azure Function** (worker isolado, trigger RabbitMQ) | 3 | **Histórico**: nunca foi implantada na nuvem; não faz parte do ambiente de produção |

No caminho obrigatório da Fase 4 (RabbitMQ no cluster) **não há serviço de notificação em produção**: os eventos
`UserCreated`/`PaymentProcessed` ficam sem assinante de notificação, e Catalog e Payments seguem consumindo
normalmente. A Lambda abaixo é o caminho opcional para ter notificações.

## Lambda + SQS/SNS (Fase 4, opcional)

Fluxo: Users/Catalog/Payments publicam via MassTransit em **SNS** (`Messaging:Transport=AmazonSqs`) -> assinatura
SNS com *raw delivery* -> fila **SQS** `arcadelink-notifications` (com **DLQ** após 3 tentativas) -> **Lambda**
`arcadelink-notifications` (`dotnet8`, 256 MB, timeout 15 s), que processa o lote e reporta falhas parciais
(`ReportBatchItemFailures`) — só as mensagens com erro voltam à fila. Os logs vão para o CloudWatch
(retenção de 7 dias). A Lambda desembrulha o envelope do MassTransit (`Envelope/`) e usa os contratos em
`Contracts/` (cópias dos eventos, como nos demais repos).

Os tópicos SNS (`arcadelink-user-created`, `arcadelink-payment-processed`) são criados pela stack
`ArcadeLink.Orchestration/terraform/env` com `enable_messaging=true`; esta stack só os referencia.

```bash
# 1) tópicos SNS + políticas das roles de pod (no Orchestration)
terraform -chdir=../ArcadeLink.Orchestration/terraform/env apply -var enable_messaging=true

# 2) publicar a Lambda (a partir desta pasta) — o archive_file compacta terraform/publish
dotnet publish src/ArcadeLink.NotificationsLambda -c Release -r linux-x64 --self-contained false -o terraform/publish

# 3) SQS + DLQ + assinaturas + Lambda
cd terraform && terraform init && terraform apply

# 4) no Orchestration: variável MESSAGING_TRANSPORT=AmazonSqs e rodar o workflow deploy-platform
#    (cria o ConfigMap messaging-config, reinicia as APIs e remove o RabbitMQ do cluster)
```

Ver os logs: `aws logs tail $(terraform output -raw log_group_name) --follow --region us-east-2`
(outputs: `queue_url`, `dlq_url`, `lambda_function_name`, `log_group_name`). Para voltar ao RabbitMQ:
`MESSAGING_TRANSPORT=RabbitMq` + `deploy-platform`; depois `terraform destroy` aqui. O custo no volume da
demo é ~zero, mas destrua junto com o ambiente.

Testes: `dotnet test` (inclui `tests/ArcadeLink.NotificationsLambda.Tests`, sem precisar de AWS).

## Azure Function (Fase 3 — histórico)

Mantida como registro da Fase 3, quando o `ArcadeLink.Notifications` (Web API da Fase 2) foi substituído por uma
Function acionada por trigger de mensageria. Não é implantada nem necessária na Fase 4.

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

## Execução local da Azure Function (apenas para estudo)

Não há mais script de criação de filas (o auxiliar da Fase 3 foi removido do Orchestration). Para
reproduzir o fluxo da Fase 3 é preciso declarar manualmente as filas `function-user-created-event` e
`function-payment-processed-event` no RabbitMQ (bind nos exchanges `ArcadeLink.Contracts:UserCreatedEvent` e
`ArcadeLink.Contracts:PaymentProcessedEvent`) e usar o Azure Functions Core Tools v4 com
`local.settings.json` criado a partir do `local.settings.json.example` (gitignored; use credenciais próprias,
não versione). O host real do Functions com o binding RabbitMQ nunca foi validado nesta entrega.

## Testes

```bash
dotnet test
```

`tests/ArcadeLink.NotificationsFunction.Tests` (xUnit): `WelcomeEmailFunctionTests` e
`PurchaseConfirmationFunctionTests`, cobrindo o caminho de sucesso, o no-op em `Rejected` (só na
confirmação de compra) e o envelope malformado — sem precisar de RabbitMQ real, já que as Functions
recebem a mensagem já como `string` (o parâmetro do trigger) e são chamadas diretamente nos testes.
