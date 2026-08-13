using System.Text.Json;
using System.Text.Json.Serialization;
using MX.Domain.Tickets;

namespace MX.Infrastructure.Persistence;

/// <summary>
/// Reads and writes <see cref="TicketStatus"/> as the dataset's display strings.
///
/// Without this, System.Text.Json would emit the C# member name "InProgress" and
/// fail to read the file's "In Progress". The mismatch is silent — it surfaces as
/// a deserialization error or, worse, a rewritten dataset — which is why the
/// round-trip is pinned by a test against the real file.
/// </summary>
public sealed class TicketStatusJsonConverter : JsonConverter<TicketStatus>
{
    public override TicketStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.GetString();

        return TicketStatusNames.TryParse(raw, out var status)
            ? status
            : throw new JsonException(
                $"'{raw}' is not a known ticket status. Expected one of: {string.Join(", ", TicketStatusNames.All)}.");
    }

    public override void Write(Utf8JsonWriter writer, TicketStatus value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToDisplayName());
}
