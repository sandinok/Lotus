using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using Avalonia.Threading;
using Lotus.Models;

namespace Lotus.Services;

public sealed class ConfigManager : IDisposable
{
    private readonly string _configPath;
    private readonly FileSystemWatcher _watcher;
    private readonly Timer _saveTimer;
    private AppConfig _config = new();
    private readonly object _lock = new();
    private bool _pendingSave;

    public AppConfig Config 
    { 
        get { lock(_lock) return _config; } 
        private set { lock(_lock) _config = value; }
    }

    public event Action<AppConfig>? OnConfigChanged;

    public ConfigManager()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "Lotus");
        Directory.CreateDirectory(dir);
        _configPath = Path.Combine(dir, "content.json");
        
        Load();
        
        _watcher = new FileSystemWatcher(dir, "content.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
        };
        _watcher.Changed += (s, e) => Dispatcher.UIThread.Post(Load);
        _watcher.EnableRaisingEvents = true;
        
        _saveTimer = new Timer(_ => FlushSave(), null, Timeout.Infinite, Timeout.Infinite);
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                Save();
                return;
            }
            var json = File.ReadAllText(_configPath);
            var cfg = JsonSerializer.Deserialize<AppConfig>(json);
            if (cfg != null)
            {
                Config = cfg;
                OnConfigChanged?.Invoke(Config);
            }
        }
        catch { /* Ignore malformed JSON */ }
    }

    public void Update(Action<AppConfig> modifier)
    {
        modifier(Config);
        _pendingSave = true;
        _saveTimer.Change(1000, Timeout.Infinite);
    }

    private void FlushSave()
    {
        if (!_pendingSave) return;
        Save();
        _pendingSave = false;
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }
        catch { }
    }

    public void Dispose()
    {
        _saveTimer.Dispose();
        _watcher.Dispose();
        FlushSave();
    }
}
