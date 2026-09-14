using ArcadeLink.NotificationsFunction.Api.Functions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ArcadeLink.NotificationsFunction.Tests;

public class WelcomeEmailFunctionTests
{
    private static ILogger<WelcomeEmailFunction> CreateLogger(CapturingLoggerProvider provider) =>
        LoggerFactory.Create(b => b.AddProvider(provider)).CreateLogger<WelcomeEmailFunction>();

    [Fact]
    public void Logs_welcome_message_when_envelope_unwraps_successfully()
    {
        var logs = new CapturingLoggerProvider();
        var function = new WelcomeEmailFunction(CreateLogger(logs));
        var envelope = """{"message":{"userId":"11111111-1111-1111-1111-111111111111","name":"Ada Lovelace","email":"ada@example.com"}}""";

        function.Run(envelope);

        Assert.Contains(logs.Messages, m => m.Contains("boas-vindas") && m.Contains("ada@example.com"));
    }

    [Fact]
    public void Logs_warning_when_envelope_cannot_be_unwrapped()
    {
        var logs = new CapturingLoggerProvider();
        var function = new WelcomeEmailFunction(CreateLogger(logs));

        function.Run("not valid json");

        Assert.Contains(logs.Messages, m => m.Contains("Não foi possível desembrulhar"));
    }
}
