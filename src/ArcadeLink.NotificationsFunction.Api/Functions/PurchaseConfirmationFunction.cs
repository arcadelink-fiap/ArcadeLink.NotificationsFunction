using ArcadeLink.Contracts;
using ArcadeLink.NotificationsFunction.Api.Envelope;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ArcadeLink.NotificationsFunction.Api.Functions;

public class PurchaseConfirmationFunction(ILogger<PurchaseConfirmationFunction> logger)
{
    [Function(nameof(PurchaseConfirmationFunction))]
    public void Run(
        [RabbitMQTrigger("function-payment-processed-event", ConnectionStringSetting = "RabbitMqConnection")] string message)
    {
        var evt = MassTransitEnvelopeReader.Unwrap<PaymentProcessedEvent>(message);
        if (evt is null)
        {
            logger.LogWarning("Não foi possível desembrulhar PaymentProcessedEvent do envelope recebido: {Message}", message);
            return;
        }

        if (evt.Status != "Approved")
            return;

        logger.LogInformation(
            "Confirmação de compra enviada para o usuário {UserId} (jogo {GameId}, pedido {OrderId})",
            evt.UserId, evt.GameId, evt.OrderId);
    }
}
