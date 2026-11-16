using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using ArcadeLink.Contracts;
using ArcadeLink.NotificationsLambda.Envelope;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ArcadeLink.NotificationsLambda;

public class Function
{
    private const string UserCreatedType = "urn:message:ArcadeLink.Contracts:UserCreatedEvent";
    private const string PaymentProcessedType = "urn:message:ArcadeLink.Contracts:PaymentProcessedEvent";

    public SQSBatchResponse FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        var failures = new List<SQSBatchResponse.BatchItemFailure>();

        foreach (var record in sqsEvent.Records ?? [])
        {
            try
            {
                Process(record.Body, context.Logger);
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"Falha ao processar a mensagem {record.MessageId}: {ex}");
                failures.Add(new SQSBatchResponse.BatchItemFailure { ItemIdentifier = record.MessageId });
            }
        }

        return new SQSBatchResponse { BatchItemFailures = failures };
    }

    // Mensagem malformada ou de tipo desconhecido é reconhecida (warning), como na Azure Function.
    private static void Process(string? body, ILambdaLogger logger)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            logger.LogWarning("Mensagem SQS sem corpo; ignorada");
            return;
        }

        switch (MassTransitEnvelopeReader.ReadMessageType(body))
        {
            case UserCreatedType:
                HandleUserCreated(body, logger);
                break;
            case PaymentProcessedType:
                HandlePaymentProcessed(body, logger);
                break;
            case var other:
                logger.LogWarning($"Tipo de mensagem desconhecido ou envelope inválido ({other ?? "sem messageType"}): {body}");
                break;
        }
    }

    private static void HandleUserCreated(string body, ILambdaLogger logger)
    {
        var evt = MassTransitEnvelopeReader.Unwrap<UserCreatedEvent>(body);
        if (evt is null)
        {
            logger.LogWarning($"Não foi possível desembrulhar UserCreatedEvent do envelope recebido: {body}");
            return;
        }

        logger.LogInformation($"E-mail de boas-vindas enviado para {evt.Email}");
    }

    private static void HandlePaymentProcessed(string body, ILambdaLogger logger)
    {
        var evt = MassTransitEnvelopeReader.Unwrap<PaymentProcessedEvent>(body);
        if (evt is null)
        {
            logger.LogWarning($"Não foi possível desembrulhar PaymentProcessedEvent do envelope recebido: {body}");
            return;
        }

        if (evt.Status != "Approved")
            return;

        logger.LogInformation(
            $"Confirmação de compra enviada para o usuário {evt.UserId} (jogo {evt.GameId}, pedido {evt.OrderId})");
    }
}
