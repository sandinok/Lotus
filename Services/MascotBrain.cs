using System;
using Microsoft.UI.Xaml;

namespace Lotus.Services;

/// <summary>
/// Mascot behavior controller - Handles idle animations and breathing exercises
/// Thread-safe singleton with proper resource cleanup
/// </summary>
public sealed class MascotBrain : IDisposable
{
    private static readonly Lazy<MascotBrain> _instance = new(() => new MascotBrain());
    public static MascotBrain Instance => _instance.Value;

    private readonly DispatcherTimer _bioTimer;
    private DateTime _lastStretchTime;
    private DispatcherTimer? _breathingTimer;
    private bool _isBreathingSessionActive;
    private bool _isDisposed;
    private readonly Random _random = new();
    
    public event EventHandler<string>? RequestAnimation; 
    public event EventHandler<string>? RequestThought;

    private MascotBrain()
    {
        _bioTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _bioTimer.Tick += BioTimer_Tick;
        _bioTimer.Start();
        _lastStretchTime = DateTime.Now;
    }

    private void BioTimer_Tick(object? sender, object e)
    {
        if (_isBreathingSessionActive || _isDisposed) return;

        var now = DateTime.Now;
        var hour = now.Hour;

        // Posture nudge every 20 mins
        if ((now - _lastStretchTime).TotalMinutes >= 20)
        {
            RequestAnimation?.Invoke(this, "Stretch");
            RequestThought?.Invoke(this, "Time to stretch!");
            _lastStretchTime = now;
            return;
        }

        // Late night stargazing (22:00-05:00)
        if (hour >= 22 || hour < 5)
        {
            if (_random.Next(0, 10) == 0)
            {
                RequestAnimation?.Invoke(this, "Stargaze");
                RequestThought?.Invoke(this, "Looking at the stars...");
            }
        }
        // Morning yawn (07:00-07:30)
        else if (hour == 7 && now.Minute < 30)
        {
            if (_random.Next(0, 5) == 0)
            {
                RequestAnimation?.Invoke(this, "Yawn");
                RequestThought?.Invoke(this, "Good morning!");
            }
        }
    }

    public void StartBreathingSession()
    {
        if (_isBreathingSessionActive || _isDisposed) return;
        _isBreathingSessionActive = true;
        
        // Stop existing timer if any (prevents leak)
        _breathingTimer?.Stop();
        
        RequestAnimation?.Invoke(this, "BreathingStart");
        RequestThought?.Invoke(this, "Breathe with me...");
        
        // Single reusable timer
        _breathingTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(60)
        };
        _breathingTimer.Tick += (s, e) => 
        {
            _isBreathingSessionActive = false;
            RequestAnimation?.Invoke(this, "BreathingEnd");
            RequestThought?.Invoke(this, "Feeling better?");
            _breathingTimer?.Stop();
        };
        _breathingTimer.Start();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        
        _bioTimer.Stop();
        _breathingTimer?.Stop();
    }
}

