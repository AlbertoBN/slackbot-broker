using System.Text.Json;
using SlackBotBroker.Protocol;

namespace SlackBotBroker.Protocol.Tests;

public class ProtocolJsonContextTests
{
    private sealed record NotRegisteredWithTheContext(int Value);

    [Fact]
    public void Envelope_type_is_resolved_by_the_generated_context()
    {
        Assert.NotNull(ProtocolJsonContext.Default.MessageEnvelope);
        Assert.NotNull(ProtocolJsonContext.Default.GetTypeInfo(typeof(MessageEnvelope)));
    }

    [Fact]
    public void Serializing_an_unregistered_type_through_the_context_options_throws_instead_of_falling_back_to_reflection()
    {
        // ProtocolJsonContext.Default.Options resolves types solely through source generation.
        // If a reflection-based fallback were silently in play, this would succeed instead of throwing.
        Assert.Throws<NotSupportedException>(() =>
            JsonSerializer.Serialize(new NotRegisteredWithTheContext(1), ProtocolJsonContext.Default.Options));
    }
}
