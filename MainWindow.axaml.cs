using System;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Lotus.Services;
using Avalonia;
using System.Linq;
using System.Numerics;
using Lotus.Models;
using Lotus.Controls;

namespace Lotus;

public partial class MainWindow : Window
{
    private readonly ConfigManager _config;
    private readonly MascotEngine _mascot;
    private readonly AudioGuardian _audio;
    private Point _dragStart;

    public MainWindow()
    {
        InitializeComponent();
        
        _config = new ConfigManager();
        _mascot = new MascotEngine(_config);
        _audio = new AudioGuardian();
        
        // Conectar el control con el engine
        if (MascotControl != null)
            MascotControl.Engine = _mascot;
        
        SetupEvents();
        ApplyTheme();
        
        Dispatcher.UIThread.Post(async () =>
        {
            await System.Threading.Tasks.Task.Delay(1000);
            var msg = _config.Config.DailyMessages[new Random().Next(_config.Config.DailyMessages.Count)];
            ShowMessage(msg);
        });
    }

    private void SetupEvents()
    {
        PointerPressed += (s, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                if (MascotControl != null)
                {
                    _dragStart = e.GetPosition(this);
                    // Click relativo al control
                    var pos = e.GetPosition(MascotControl);
                    _mascot.OnClick(pos); 
                    BeginMoveDrag(e);
                }
            }
        };
        
        PointerMoved += (s, e) => 
        {
            if (MascotControl != null)
                _mascot?.OnMouseMove(e.GetPosition(MascotControl));
        };
        
        _mascot.OnPropertyChanged += () => Dispatcher.UIThread.Post(() => MascotControl?.InvalidateVisual());
        _mascot.OnMessage += msg => Dispatcher.UIThread.Post(() => ShowMessage(msg));
        
        _audio.OnSpectrumUpdate += bands => Dispatcher.UIThread.Post(() => UpdateSpectrum(bands));
        _audio.OnLimitTriggered += () => Dispatcher.UIThread.Post(() => _mascot.TriggerAlert());
        
        _config.OnConfigChanged += _ => Dispatcher.UIThread.Post(ApplyTheme);
        
        Closing += (s, e) => { _mascot?.Dispose(); _audio?.Dispose(); _config?.Dispose(); };
    }

    private void UpdateSpectrum(double[] bands)
    {
        if (Bar0 == null) return;
        Bar0.Value = Math.Min(100, bands[0] * 300);
        Bar1.Value = Math.Min(100, bands[1] * 300);
        Bar2.Value = Math.Min(100, bands[2] * 300);
        Bar3.Value = Math.Min(100, bands[3] * 300);
        Bar4.Value = Math.Min(100, bands[4] * 300);
        
        Bar0.Foreground = BandBrush(bands[0]);
        Bar1.Foreground = BandBrush(bands[1]);
        Bar2.Foreground = BandBrush(bands[2]);
        Bar3.Foreground = BandBrush(bands[3]);
        Bar4.Foreground = BandBrush(bands[4]);
    }

    private IBrush BandBrush(double val)
    {
        var intensity = Math.Min(1.0, val * 4);
        if (intensity > 0.8) return new SolidColorBrush(Color.Parse("#FF4444"));
        if (intensity > 0.5) return new SolidColorBrush(Color.Parse("#FFAA00"));
        return new SolidColorBrush(Color.Parse("#44FF44"));
    }

    private void ShowMessage(string msg)
    {
        if (MessageText == null || MessageBorder == null) return;
        MessageText.Text = msg;
        MessageBorder.Opacity = 1;
        
        Dispatcher.UIThread.Post(async () =>
        {
            try 
            {
                await System.Threading.Tasks.Task.Delay(4000);
                if (MessageBorder != null)
                    MessageBorder.Opacity = 0;
            }
            catch { }
        });
    }

    private void ApplyTheme()
    {
        var theme = _config.Config.Themes[_config.Config.CurrentTheme];
        if (Resources["PrimaryBrush"] is SolidColorBrush primary)
            primary.Color = Color.Parse(theme.Primary);
        if (Resources["SecondaryBrush"] is SolidColorBrush secondary)
            secondary.Color = Color.Parse(theme.Secondary);
        if (Resources["AccentBrush"] is SolidColorBrush accent)
            accent.Color = Color.Parse(theme.Accent);
    }

    public void FeedButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _mascot.IsFeedingMode = !_mascot.IsFeedingMode;
        if (FeedIndicator != null)
            FeedIndicator.IsVisible = _mascot.IsFeedingMode;
        ShowMessage(_mascot.IsFeedingMode ? "🍃 Click mascot to feed!" : "Feeding mode off");
    }

    public void RestButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var cfg = _config.Config;
        cfg.MascotState.Energy = Math.Min(100, cfg.MascotState.Energy + 30);
        _config.Update(c => { });
        ShowMessage("💤 Resting... Energy restored!");
    }
}
