using Amazon.Lambda.SQSEvents;
using Amazon.Lambda.TestUtilities;
using ArcadeLink.NotificationsLambda;

namespace ArcadeLink.NotificationsLambda.Tests;

public class FunctionTests
{
    private static string Fixture(string name) => File.ReadAllText(Path.Combine("Fixtures", name + ".json"));

    private static SQSEvent.SQSMessage Msg(string id, string body) => new() { MessageId = id, Body = body };

    private static (SQSBatchResponse Response, string Logs) Run(params SQSEvent.SQSMessage[] messages)
    {
        var context = new TestLambdaContext();
        var response = new Function().FunctionHandler(new SQSEvent { Records = messages.ToList() }, context);
        return (response, ((TestLambdaLogger)context.Logger).Buffer.ToString());
    }

    [Fact]
    public void Logs_welcome_for_user_created()
    {
        var (response, logs) = Run(Msg("m1", Fixture("user-created")));

        Assert.Contains("E-mail de boas-vindas enviado para ana@arcadelink.com", logs);
        Assert.Empty(response.BatchItemFailures);
    }

    [Fact]
    public void Logs_confirmation_for_approved_payment()
    {
        var (response, logs) = Run(Msg("m1", Fixture("payment-approved")));

        Assert.Contains(
            "Confirmação de compra enviada para o usuário 11111111-1111-1111-1111-111111111111 (jogo 33333333-3333-3333-3333-333333333333, pedido 22222222-2222-2222-2222-222222222222)",
            logs);
        Assert.Empty(response.BatchItemFailures);
    }

    [Fact]
    public void Ignores_rejected_payment()
    {
        var (response, logs) = Run(Msg("m1", Fixture("payment-rejected")));

        Assert.DoesNotContain("Confirmação de compra", logs);
        Assert.Empty(response.BatchItemFailures);
    }

    [Fact]
    public void Malformed_message_is_acknowledged()
    {
        var (response, logs) = Run(Msg("m1", "isto não é json {"));

        Assert.Contains("Warning:", logs);
        Assert.Empty(response.BatchItemFailures);
    }

    [Fact]
    public void Unknown_type_is_acknowledged()
    {
        var (response, logs) = Run(Msg("m1", Fixture("unknown-type")));

        Assert.Contains("Warning:", logs);
        Assert.Contains("SomethingElse", logs);
        Assert.Empty(response.BatchItemFailures);
    }

    [Fact]
    public void Processes_mixed_batch()
    {
        var (response, logs) = Run(
            Msg("m1", Fixture("user-created")),
            Msg("m2", Fixture("payment-approved")),
            Msg("m3", Fixture("payment-rejected")));

        Assert.Contains("E-mail de boas-vindas", logs);
        Assert.Contains("Confirmação de compra", logs);
        Assert.Empty(response.BatchItemFailures);
    }

    [Fact]
    public void Null_body_is_acknowledged_and_does_not_break_batch()
    {
        var (response, _) = Run(Msg("m1", Fixture("user-created")), new SQSEvent.SQSMessage { MessageId = "m2", Body = null });

        // corpo nulo é tratado como malformado (reconhecido), nunca derruba o lote
        Assert.Empty(response.BatchItemFailures);
    }
}
