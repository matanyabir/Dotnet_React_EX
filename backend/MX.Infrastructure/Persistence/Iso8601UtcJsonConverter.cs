using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MX.Infrastructure.Persistence;

/// <summary>
/// Writes timestamps in the dataset's own shape: UTC, second precision, "Z" suffix
/// (e.g. <c>2025-10-27T14:35:00Z</c>).
///
/// The default converter would emit <c>+00:00</c> and seven fractional digits,
/// which parses back fine but rewrites every row the first time the file is saved.
/// Matching the existing format keeps diffs limited to tickets that actually changed.
/// </summary>
public sealed class Iso8601UtcJsonConverter : JsonConverter<DateTimeOffset>
{
    private const string Format = "yyyy-MM-ddTHH:mm:ssZ";

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.GetString();

        return DateTimeOffset.TryParse(
            raw,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : throw new JsonException($"'{raw}' is not a valid ISO-8601 timestamp.");
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToUniversalTime().ToString(Format, CultureInfo.InvariantCulture));
}
