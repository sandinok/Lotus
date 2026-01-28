using System.Text.Json.Serialization;

namespace Lotus.Models;

/// <summary>
/// Root configuration model matching content.json schema
/// </summary>
public sealed class AppConfiguration
{
    [JsonPropertyName("daily_messages")]
    public List<string> DailyMessages { get; set; } = new();

    [JsonPropertyName("him_note")]
    public string HimNote { get; set; } = string.Empty;

    [JsonPropertyName("eli_note")]
    public string EliNote { get; set; } = string.Empty;

    [JsonPropertyName("secret_messages")]
    public List<string> SecretMessages { get; set; } = new();

    [JsonPropertyName("audio")]
    public AudioSettings Audio { get; set; } = new();

    [JsonPropertyName("themes")]
    public Dictionary<string, ThemeDefinition> Themes { get; set; } = new();

    [JsonPropertyName("current_theme")]
    public string CurrentTheme { get; set; } = "garden";
}

/// <summary>
/// Audio guardian configuration
/// </summary>
public sealed class AudioSettings
{
    [JsonPropertyName("threshold")]
    public double Threshold { get; set; } = 0.6;

    [JsonPropertyName("safe_volume")]
    public double SafeVolume { get; set; } = 0.35;

    [JsonPropertyName("recovery_ms")]
    public int RecoveryMs { get; set; } = 3000;
}

/// <summary>
/// Theme color definition
/// </summary>
public sealed class ThemeDefinition
{
    [JsonPropertyName("primary")]
    public string Primary { get; set; } = "#4CAF50";
    
    [JsonPropertyName("secondary")]
    public string Secondary { get; set; } = "#81C784";
    
    [JsonPropertyName("accent")]
    public string Accent { get; set; } = "#388E3C";
}
