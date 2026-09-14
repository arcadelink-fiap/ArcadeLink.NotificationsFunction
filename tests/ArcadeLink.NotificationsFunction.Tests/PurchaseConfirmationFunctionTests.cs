using ArcadeLink.NotificationsFunction.Api.Functions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ArcadeLink.NotificationsFunction.Tests;

public class PurchaseConfirmationFunctionTests
{
    private static ILogger<PurchaseConfirmationFunction> CreateLogger(CapturingLoggerProvider provider) =>
        LoggerFactory.Create(b => b.AddProvider(provider)).CreateLogger<PurchaseConfirmationFunction>();

    [Fact]
    public void Approved_logs_purchase_confirmation()
    {
        var logs = new CapturingLoggerProvider();
        var function = new PurchaseConfirmationFunction(CreateLogger(logs));
        var orderId = Guid.NewGuid();
        var envelope = $$$"""{"message":{"orderId":"{{{orderId}}}","userId":"22222222-2222-2222-2222-222222222222","gameId":"33333333-3333-3333-3333-333333333333","status":"Approved"}}""";

        function.Run(envelope);

        Assert.Contains(logs.Messages, m => m.Contains("Confirmação") && m.Contains(orderId.ToString()));
    }

    [Fact]
    public void Rejected_logs_nothing()
    {
        var logs = new CapturingLoggerProvider();
        var function = new PurchaseConfirmationFunction(CreateLogger(logs));
        var orderId = Guid.NewGuid();
        var envelope = $$$"""{"message":{"orderId":"{{{orderId}}}","userId":"22222222-2222-2222-2222-222222222222","gameId":"33333333-3333-3333-3333-333333333333","status":"Rejected"}}""";

        function.Run(envelope);

        Assert.DoesNotContain(logs.Messages, m => m.Contains(orderId.ToString()));
    }

    [Fact]
    public void Logs_warning_when_envelope_cannot_be_unwrapped()
    {
        var logs = new CapturingLoggerProvider();
        var function = new PurchaseConfirmationFunction(CreateLogger(logs));

        function.Run("not valid json");

        Assert.Contains(logs.Messages, m => m.Contains("Não foi possível desembrulhar"));
    }
}
