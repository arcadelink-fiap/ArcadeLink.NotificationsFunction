using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArcadeLink.NotificationsLambda.Envelope;

public class MassTransitEnvelope<T>
{
    [JsonPropertyName("message")]
    public T? Message { get; set; }
}

public class MassTransitEnvelopeHeader
{
    [JsonPropertyName("messageType")]
    public List<string>? MessageType { get; set; }
}

public static class MassTransitEnvelopeReader
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static T? Unwrap<T>(string rawJson)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<MassTransitEnvelope<T>>(rawJson, Options);
            return envelope is null ? default : envelope.Message;
        }
        catch (JsonException)
        {
            return default;
        }
    }

    /// <summary>Primeiro item de <c>messageType</c> (ex.: "urn:message:ArcadeLink.Contracts:UserCreatedEvent"), ou null.</summary>
    public static string? ReadMessageType(string rawJson)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<MassTransitEnvelopeHeader>(rawJson, Options);
            return envelope?.MessageType?.FirstOrDefault();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
