using System.Text.Json;
using Lotus.Models;

namespace Lotus.Services;

/// <summary>
/// Handles loading and saving of content.json configuration
/// Thread-safe singleton with debounced auto-save
/// </summary>
public sealed class ConfigurationService
{
    private static readonly Lazy<ConfigurationService> _instance = new(() => new ConfigurationService());
    public static ConfigurationService Instance => _instance.Value;

    private readonly string _configPath;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;
    private CancellationTokenSource? _debounceToken;
    private const int DebounceMs = 500;

    public AppConfiguration Config { get; private set; } = new();

    public event EventHandler? ConfigurationChanged;

    private ConfigurationService()
    {
        // Get config path next to executable
        var exeDir = AppContext.BaseDirectory;
        _configPath = Path.Combine(exeDir, "Assets", "content.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Load configuration from disk asynchronously
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = await File.ReadAllTextAsync(_configPath);
                Config = JsonSerializer.Deserialize<AppConfiguration>(json, _jsonOptions) ?? new AppConfiguration();
            }
            else
            {
                // Create default config
                Config = CreateDefaultConfig();
                await SaveAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConfigService] Load error: {ex.Message}");
            Config = CreateDefaultConfig();
        }
    }

    /// <summary>
    /// Save configuration to disk asynchronously (with timeout protection)
    /// </summary>
    public async Task SaveAsync()
    {
        // 5 second timeout to prevent deadlock
        if (!await _saveLock.WaitAsync(TimeSpan.FromSeconds(5)))
        {
            System.Diagnostics.Debug.WriteLine("[ConfigService] Save timeout - lock held too long");
            return;
        }
        
        try
        {
            var json = JsonSerializer.Serialize(Config, _jsonOptions);
            await File.WriteAllTextAsync(_configPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConfigService] Save error: {ex.Message}");
        }
        finally
        {
            _saveLock.Release();
        }
    }

    /// <summary>
    /// Debounced save - waits for typing to stop before saving
    /// </summary>
    public void SaveDebounced()
    {
        _debounceToken?.Cancel();
        _debounceToken = new CancellationTokenSource();
        var token = _debounceToken.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DebounceMs, token);
                if (!token.IsCancellationRequested)
                {
                    await SaveAsync();
                }
            }
            catch (TaskCanceledException)
            {
                // Expected when debouncing
            }
        }, token);
    }

    /// <summary>
    /// Update Eli's note with debounced save
    /// </summary>
    public void UpdateEliNote(string note)
    {
        Config.EliNote = note;
        SaveDebounced();
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Get a random daily message
    /// </summary>
    public string GetRandomDailyMessage()
    {
        if (Config.DailyMessages.Count == 0)
            return "Hello! 💚";
        
        var index = Random.Shared.Next(Config.DailyMessages.Count);
        return Config.DailyMessages[index];
    }

    /// <summary>
    /// Get a random secret message for easter eggs
    /// </summary>
    public string GetRandomSecretMessage()
    {
        if (Config.SecretMessages.Count == 0)
            return "💚";
        
        var index = Random.Shared.Next(Config.SecretMessages.Count);
        return Config.SecretMessages[index];
    }

    private static AppConfiguration CreateDefaultConfig()
    {
        return new AppConfiguration
        {
            DailyMessages = new List<string>
            {
                "Good morning Eli!",
                "You look pretty!",
                "Made for you 💚"
            },
            HimNote = "Drink water 💧",
            EliNote = "",
            SecretMessages = new List<string>
            {
                "I love the way you smile",
                "You make my days softer",
                "Mahal kita 💚",
                "Ingat ka palagi",
                "Andito lang ako"
            },
            Audio = new AudioSettings
            {
                Threshold = 0.6,
                SafeVolume = 0.35,
                RecoveryMs = 3000
            },
            Themes = new Dictionary<string, ThemeDefinition>
            {
                { "garden", new ThemeDefinition { Primary = "#00ff88" } },
                { "ocean", new ThemeDefinition { Primary = "#00ffff" } },
                { "sakura", new ThemeDefinition { Primary = "#ffc0cb" } },
                { "matcha", new ThemeDefinition { Primary = "#a8e6cf" } },
                { "rain", new ThemeDefinition { Primary = "#4682b4" } },
                { "aurora", new ThemeDefinition { Primary = "#2d0036" } }
            },
            CurrentTheme = "garden"
        };
    }
}
