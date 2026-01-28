using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Lotus.Models;

namespace Lotus.Services;

public sealed class ThemeService
{
    private static readonly Lazy<ThemeService> _instance = new(() => new ThemeService());
    public static ThemeService Instance => _instance.Value;

    private string _currentTheme = "forest";
    private SolidColorBrush _currentPrimaryBrush = new(Color.FromArgb(255, 34, 197, 94));
    
    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    private ThemeService() { }

    public string CurrentTheme => _currentTheme;

    public void SetTheme(string themeName)
    {
        var config = ConfigurationService.Instance.Config;
        if (!config.Themes.ContainsKey(themeName)) return;
        if (_currentTheme == themeName) return;

        _currentTheme = themeName;
        
        var theme = config.Themes[themeName];
        var primary = ParseHexColor(theme.Primary);
        var secondary = ParseHexColor(theme.Secondary);
        var accent = ParseHexColor(theme.Accent);
        
        // Update brushes
        UpdateBrush("AeroAccentPrimaryBrush", primary);
        UpdateBrush("AeroAccentLightBrush", secondary);
        UpdateBrush("AeroAccentDarkBrush", accent);
        
        // Update gradients
        RebuildHeaderGradient(primary, accent);
        RebuildAudioBarGradient(primary, secondary);
        
        // Cache current brush
        _currentPrimaryBrush = new SolidColorBrush(primary);
        
        // Persist
        config.CurrentTheme = themeName;
        _ = ConfigurationService.Instance.SaveAsync();

        ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(themeName, primary));
    }

    private void UpdateBrush(string key, Color color)
    {
        var resources = Application.Current.Resources;
        if (resources.TryGetValue(key, out var resource) && resource is SolidColorBrush brush)
        {
            brush.Color = color;
        }
        else
        {
            resources[key] = new SolidColorBrush(color);
        }
    }

    private void RebuildHeaderGradient(Color primary, Color accent)
    {
        var resources = Application.Current.Resources;
        
        var gradient = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(0, 1)
        };
        gradient.GradientStops.Add(new GradientStop { Color = primary, Offset = 0 });
        gradient.GradientStops.Add(new GradientStop { Color = accent, Offset = 1 });
        
        resources["AeroGreenHeaderGradient"] = gradient;
    }
    
    private void RebuildAudioBarGradient(Color primary, Color secondary)
    {
        var resources = Application.Current.Resources;
        
        var gradient = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(1, 0)
        };
        gradient.GradientStops.Add(new GradientStop { Color = primary, Offset = 0 });
        gradient.GradientStops.Add(new GradientStop { Color = Blend(primary, secondary, 0.5f), Offset = 0.6 });
        gradient.GradientStops.Add(new GradientStop { Color = secondary, Offset = 1 });
        
        resources["AeroLiquidBarGradient"] = gradient;
    }

    public void Initialize()
    {
        var config = ConfigurationService.Instance.Config;
        
        // Night mode check
        int hour = DateTime.Now.Hour;
        bool isNight = hour >= 22 || hour < 6;
        
        if (isNight && config.Themes.ContainsKey("midnight"))
        {
            _currentTheme = "midnight";
        }
        else
        {
            _currentTheme = config.CurrentTheme ?? "forest";
        }
        
        // Apply
        if (config.Themes.ContainsKey(_currentTheme))
        {
            var theme = config.Themes[_currentTheme];
            var primary = ParseHexColor(theme.Primary);
            var secondary = ParseHexColor(theme.Secondary);
            var accent = ParseHexColor(theme.Accent);
            
            UpdateBrush("AeroAccentPrimaryBrush", primary);
            UpdateBrush("AeroAccentLightBrush", secondary);
            UpdateBrush("AeroAccentDarkBrush", accent);
            RebuildHeaderGradient(primary, accent);
            RebuildAudioBarGradient(primary, secondary);
            
            _currentPrimaryBrush = new SolidColorBrush(primary);
        }
    }

    public Color GetCurrentPrimaryColor()
    {
        return _currentPrimaryBrush.Color;
    }
    
    public SolidColorBrush GetCurrentPrimaryBrush()
    {
        return _currentPrimaryBrush;
    }

    private static Color ParseHexColor(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
            {
                return Color.FromArgb(255,
                    Convert.ToByte(hex.Substring(0, 2), 16),
                    Convert.ToByte(hex.Substring(2, 2), 16),
                    Convert.ToByte(hex.Substring(4, 2), 16));
            }
        }
        catch { }
        return Color.FromArgb(255, 34, 197, 94);
    }
    
    private static Color Blend(Color a, Color b, float t)
    {
        return Color.FromArgb(255,
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }
}