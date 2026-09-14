using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArcadeLink.NotificationsFunction.Api.Envelope;

public class MassTransitEnvelope<T>
{
    [JsonPropertyName("message")]
    public T? Message { get; set; }
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
}
