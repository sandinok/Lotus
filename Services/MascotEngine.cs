using Lotus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using Lotus.Physics;

namespace Lotus.Services;

public enum EmotionalState
{
    Idle, Happy, Hungry, Tired, Sick, Sleep, Alert, Party, Sad
}

public sealed class Particle
{
    public Point Position;
    public Vector2 Velocity;
    public double Life;
    public IBrush? Color;
    public string Type = string.Empty;
}

public sealed class MascotEngine : IDisposable
{
    private readonly ConfigManager _config;
    private readonly VerletPhysics _physics;
    private readonly System.Timers.Timer _decayTimer;
    private readonly Random _rand = new();
    
    private readonly VerletPoint _head;
    private readonly VerletPoint _body;
    private readonly VerletStick _neck;
    
    // Ensure that these properties are public
    public double PupilOffsetX { get; private set; }
    public double PupilOffsetY { get; private set; }
    public bool IsFeedingMode 
    { 
        get => _isFeedingMode;
        set { _isFeedingMode = value; OnPropertyChanged?.Invoke(); }
    }

    private EmotionalState _state = EmotionalState.Idle;
    private bool _isBlinking;
    private readonly List<Particle> _particles = [];
    private int _consecutiveClicks;
    private DateTime _lastClick;
    private bool _isFeedingMode;

    public EmotionalState State => _state;
    public IReadOnlyList<Particle> Particles => _particles;
    public Point HeadPosition => new(_head.Position.X, _head.Position.Y);
    public Point BodyPosition => new(_body.Position.X, _body.Position.Y);
    public bool IsBlinking => _isBlinking;
    public double EyeOpenness => _isBlinking ? 0.1 : 1.0;

    public event Action? OnPropertyChanged;
    public event Action<string>? OnMessage;

    public MascotEngine(ConfigManager config)
    {
        _config = config;
        _physics = new VerletPhysics();
        
        _head = _physics.CreatePoint(new Vector2(50, 40), 0.8f);
        _body = _physics.CreatePoint(new Vector2(50, 70), 1.2f, true);
        _neck = _physics.CreateStick(_head, _body, 30f);
        
        _physics.OnUpdate += () => Dispatcher.UIThread.Post(() => OnPropertyChanged?.Invoke());
        
        _decayTimer = new System.Timers.Timer(100);
        _decayTimer.Elapsed += (_, _) => UpdateNeeds();
        _decayTimer.Start();
        
        new Thread(BlinkLoop) { IsBackground = true }.Start();
    }

    private void BlinkLoop()
    {
        while (true)
        {
            var delay = _rand.Next(2000, 4000);
            Thread.Sleep(delay);
            _isBlinking = true;
            Thread.Sleep(150);
            _isBlinking = false;
        }
    }

    private void UpdateNeeds()
    {
        var cfg = _config.Config;
        var state = cfg.MascotState;
        var now = DateTime.Now;
        var isNight = now.Hour >= 23 || now.Hour < 6;
        
        state.Hunger = Math.Min(100, state.Hunger + 0.03);
        state.Happiness = Math.Max(0, state.Happiness - 0.02);
        state.Energy = Math.Max(0, state.Energy - (isNight ? 0.05 : 0.01));
        
        if (state.Hunger > 70 || state.Energy < 30)
            state.Health = Math.Max(0, state.Health - 0.01);
        else if (state.Hunger < 50 && state.Energy > 50)
            state.Health = Math.Min(100, state.Health + 0.05);

        DetermineState(state, isNight);
        UpdateParticles();
        
        Dispatcher.UIThread.Post(() => OnPropertyChanged?.Invoke());
    }

    private void DetermineState(MascotState state, bool isNight)
    {
        if (_consecutiveClicks >= 5 && (DateTime.Now - _lastClick).TotalSeconds < 2)
        {
            _state = EmotionalState.Party;
            SpawnConfetti();
            if (_consecutiveClicks == 5) Dispatcher.UIThread.Post(() => OnMessage?.Invoke("Party time! 🎉"));
        }
        else if (state.Health < 30)
            _state = EmotionalState.Sick;
        else if (isNight && state.Energy < 20)
            _state = EmotionalState.Sleep;
        else if (state.Energy < 30)
            _state = EmotionalState.Tired;
        else if (state.Hunger > 70)
            _state = EmotionalState.Hungry;
        else if (state.Happiness > 80)
            _state = EmotionalState.Happy;
        else
            _state = EmotionalState.Idle;
            
        if (_state == EmotionalState.Sleep)
            SpawnZzz();
    }

    public void OnMouseMove(Point pos)
    {
        var dx = pos.X - _head.Position.X;
        var dy = pos.Y - _head.Position.Y;
        var dist = Math.Sqrt(dx*dx + dy*dy);
        var maxOffset = 8.0;
        
        if (dist > 0)
        {
            var factor = Math.Min(dist, 50) / 50 * maxOffset;
            PupilOffsetX = (dx / dist) * factor;
            PupilOffsetY = (dy / dist) * factor;
        }
        
        OnPropertyChanged?.Invoke();
    }

    public void OnClick(Point pos)
    {
        var now = DateTime.Now;
        if ((now - _lastClick).TotalSeconds < 1)
            _consecutiveClicks++;
        else
            _consecutiveClicks = 1;
        _lastClick = now;
        
        var cfg = _config.Config;
        cfg.MascotMemory.TotalInteractions++;
        
        if (_isFeedingMode)
        {
            cfg.MascotState.Hunger = Math.Max(0, cfg.MascotState.Hunger - 20);
            cfg.MascotState.Happiness = Math.Min(100, cfg.MascotState.Happiness + 5);
            SpawnHearts();
            _config.Update(c => { });
        }
        else
        {
            cfg.MascotState.Happiness = Math.Min(100, cfg.MascotState.Happiness + 10);
            _head.Position += new Vector2(0, -5);
            _config.Update(c => { });
        }
        
        if (pos.X < 20 && pos.Y < 20)
        {
            var msg = cfg.SecretMessages[_rand.Next(cfg.SecretMessages.Count)];
            OnMessage?.Invoke(msg);
        }
    }

    private void SpawnParticles(int count, string type, IBrush color)
    {
        for (int i = 0; i < count && _particles.Count < 20; i++)
        {
            _particles.Add(new Particle
            {
                Position = new Point(_head.Position.X + _rand.Next(-10, 10), _head.Position.Y),
                Velocity = new Vector2((float)(_rand.NextDouble() - 0.5) * 2, (float)(-1 - _rand.NextDouble())),
                Life = 1.0,
                Color = color,
                Type = type
            });
        }
    }

    private void SpawnHearts() => SpawnParticles(5, "heart", Brushes.Pink);
    private void SpawnZzz() { if (_rand.Next(10) == 0) SpawnParticles(1, "zzz", Brushes.LightBlue); }
    private void SpawnConfetti()
    {
        var colors = new IBrush[] { Brushes.Red, Brushes.Green, Brushes.Blue, Brushes.Yellow, Brushes.Purple };
        for (int i = 0; i < 10; i++)
            SpawnParticles(1, "confetti", colors[_rand.Next(colors.Length)]);
    }

    private void UpdateParticles()
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Life -= 0.02;
            
            if (p.Type == "confetti")
                p.Position = new Point(p.Position.X + p.Velocity.X, p.Position.Y + p.Velocity.Y + 0.1);
            else
                p.Position = new Point(p.Position.X + p.Velocity.X * 0.5, p.Position.Y + p.Velocity.Y);
            
            if (p.Life <= 0) _particles.RemoveAt(i);
        }
    }

    public void TriggerAlert()
    {
        _state = EmotionalState.Alert;
        OnMessage?.Invoke("🔊 Audio limit reached!");
        OnPropertyChanged?.Invoke();
    }

    public void Dispose()
    {
        _physics.Dispose();
        _decayTimer.Dispose();
    }
}
