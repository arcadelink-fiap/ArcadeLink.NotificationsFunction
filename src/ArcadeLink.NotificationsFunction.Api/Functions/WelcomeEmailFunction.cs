using ArcadeLink.Contracts;
using ArcadeLink.NotificationsFunction.Api.Envelope;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ArcadeLink.NotificationsFunction.Api.Functions;

public class WelcomeEmailFunction(ILogger<WelcomeEmailFunction> logger)
{
    [Function(nameof(WelcomeEmailFunction))]
    public void Run(
        [RabbitMQTrigger("function-user-created-event", ConnectionStringSetting = "RabbitMqConnection")] string message)
    {
        var evt = MassTransitEnvelopeReader.Unwrap<UserCreatedEvent>(message);
        if (evt is null)
        {
            logger.LogWarning("Não foi possível desembrulhar UserCreatedEvent do envelope recebido: {Message}", message);
            return;
        }

        logger.LogInformation("E-mail de boas-vindas enviado para {Email}", evt.Email);
    }
}
