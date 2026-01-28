// Models/AppConfig.cs
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Lotus.Models;

public record ThemeConfig
{
    [JsonPropertyName("primary")] public string Primary { get; set; } = "#22C55E";
    [JsonPropertyName("secondary")] public string Secondary { get; set; } = "#86EFAC";
    [JsonPropertyName("accent")] public string Accent { get; set; } = "#16A34A";
}

public record AudioConfig
{
    [JsonPropertyName("threshold")] public double Threshold { get; set; } = 0.6;
    [JsonPropertyName("safe_volume")] public double SafeVolume { get; set; } = 0.35;
    [JsonPropertyName("recovery_ms")] public int RecoveryMs { get; set; } = 3000;
    [JsonPropertyName("ambient_enabled")] public bool AmbientEnabled { get; set; } = false;
}

public record MascotMemory
{
    [JsonPropertyName("total_interactions")] public int TotalInteractions { get; set; }
    [JsonPropertyName("favorite_click_spots")] public List<string> FavoriteClickSpots { get; set; } = [];
    [JsonPropertyName("favorite_hour")] public int FavoriteHour { get; set; } = 12;
    [JsonPropertyName("average_mood")] public double AverageMood { get; set; } = 50.0;
    [JsonPropertyName("last_fed")] public DateTime LastFed { get; set; } = DateTime.UtcNow;
    [JsonPropertyName("personality")] public string Personality { get; set; } = "neutral";
    [JsonPropertyName("sleep_streak_hours")] public int SleepStreakHours { get; set; }
}

public record MascotState
{
    [JsonPropertyName("hunger")] public double Hunger { get; set; } = 50.0;
    [JsonPropertyName("happiness")] public double Happiness { get; set; } = 70.0;
    [JsonPropertyName("energy")] public double Energy { get; set; } = 80.0;
    [JsonPropertyName("health")] public double Health { get; set; } = 100.0;
}

public record AppConfig
{
    [JsonPropertyName("daily_messages")] public List<string> DailyMessages { get; set; } = ["Good morning Eli!", "You look pretty!", "Made for you 💚"];
    [JsonPropertyName("him_note")] public string HimNote { get; set; } = "Drink water 💧";
    [JsonPropertyName("eli_note")] public string EliNote { get; set; } = "";
    [JsonPropertyName("secret_messages")] public List<string> SecretMessages { get; set; } = ["I love the way you smile", "Mahal kita 💚", "Ingat ka palagi", "Andito lang ako"];
    [JsonPropertyName("audio")] public AudioConfig Audio { get; set; } = new();
    [JsonPropertyName("themes")] public Dictionary<string, ThemeConfig> Themes { get; set; } = new()
    {
        ["garden"] = new() { Primary = "#22C55E", Secondary = "#86EFAC", Accent = "#16A34A" },
        ["ocean"] = new() { Primary = "#0EA5E9", Secondary = "#7DD3FC", Accent = "#0284C7" },
        ["sakura"] = new() { Primary = "#EC4899", Secondary = "#F9A8D4", Accent = "#DB2777" },
        ["matcha"] = new() { Primary = "#84CC16", Secondary = "#BEF264", Accent = "#65A30D" },
        ["rain"] = new() { Primary = "#4682B4", Secondary = "#93C5FD", Accent = "#2563EB" },
        ["aurora"] = new() { Primary = "#8B5CF6", Secondary = "#C4B5FD", Accent = "#7C3AED" }
    };
    [JsonPropertyName("current_theme")] public string CurrentTheme { get; set; } = "garden";
    [JsonPropertyName("mascot_memory")] public MascotMemory MascotMemory { get; set; } = new();
    [JsonPropertyName("mascot_state")] public MascotState MascotState { get; set; } = new();
}