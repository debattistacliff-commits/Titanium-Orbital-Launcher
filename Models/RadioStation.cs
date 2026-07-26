using System.Text.Json.Serialization;

namespace DesktopOrbit.Models;

public sealed class RadioStation
{
    [JsonPropertyName("stationuuid")]
    public string StationUuid { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("country")]
    public string Country { get; init; } = string.Empty;

    [JsonPropertyName("tags")]
    public string Tags { get; init; } = string.Empty;

    [JsonPropertyName("url_resolved")]
    public string StreamUrl { get; init; } = string.Empty;

    public string Description => string.Join("  •  ", new[] { Country, Tags.Split(',').FirstOrDefault() }
        .Where(value => !string.IsNullOrWhiteSpace(value)));

    public string Initials
    {
        get
        {
            var words = Name.Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return "FM";
            if (words.Length == 1) return words[0].Length <= 3 ? words[0].ToUpperInvariant() : words[0][..2].ToUpperInvariant();
            return string.Concat(words.Take(3).Select(word => char.ToUpperInvariant(word[0])));
        }
    }
}
