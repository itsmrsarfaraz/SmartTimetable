using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartTimetable.Licensing;

/// <summary>The signed content of a license. A null <see cref="ExpiresUtc"/> means lifetime.</summary>
public sealed record LicensePayload
{
    [JsonPropertyName("v")] public int Version { get; init; } = 1;
    [JsonPropertyName("id")] public required Guid LicenseId { get; init; }
    [JsonPropertyName("customer")] public required string Customer { get; init; }
    [JsonPropertyName("edition")] public required string Edition { get; init; }
    [JsonPropertyName("issued")] public required DateTimeOffset IssuedUtc { get; init; }
    [JsonPropertyName("expires")] public DateTimeOffset? ExpiresUtc { get; init; }

    /// <summary>base64url of <see cref="MachineFingerprint.ToBytes"/>.</summary>
    [JsonPropertyName("machine")] public required string Machine { get; init; }

    /// <summary>How many fingerprint slots must match the current machine.</summary>
    [JsonPropertyName("min")] public required int MinMatches { get; init; }
}

internal static class LicenseJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };
}
