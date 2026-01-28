// Physics/VerletPhysics.cs
using System;
using System.Numerics;
using System.Threading;

namespace Lotus.Physics;

public sealed class VerletPoint
{
    public Vector2 Position;
    public Vector2 OldPosition;
    public Vector2 Acceleration;
    public float Mass;
    public bool Pinned;

    public VerletPoint(Vector2 pos, float mass = 1f)
    {
        Position = OldPosition = pos;
        Mass = mass;
    }

    public void Update(float dt)
    {
        if (Pinned) return;
        var velocity = Position - OldPosition;
        OldPosition = Position;
        Position += velocity + Acceleration * dt * dt;
        Acceleration = Vector2.Zero;
    }
}

public sealed class VerletStick(VerletPoint a, VerletPoint b, float length)
{
    public VerletPoint A = a;
    public VerletPoint B = b;
    public float Length = length;
    public float Stiffness = 1f;

    public void Update()
    {
        var delta = B.Position - A.Position;
        var dist = delta.Length();
        if (dist == 0) return;
        var diff = (dist - Length) / dist;
        var offset = delta * diff * 0.5f * Stiffness;
        
        if (!A.Pinned) A.Position += offset;
        if (!B.Pinned) B.Position -= offset;
    }
}

public sealed class VerletPhysics : IDisposable
{
    private readonly System.Collections.Generic.List<VerletPoint> _points = [];
    private readonly System.Collections.Generic.List<VerletStick> _sticks = [];
    private readonly CancellationTokenSource _cts = new();
    private readonly Thread _thread;
    private readonly double _timeStep = 0.016;
    
    public event Action? OnUpdate;

    public VerletPhysics()
    {
        _thread = new Thread(PhysicsLoop) { IsBackground = true };
        _thread.Start();
    }

    public VerletPoint CreatePoint(Vector2 pos, float mass = 1f, bool pinned = false)
    {
        var p = new VerletPoint(pos, mass) { Pinned = pinned };
        lock (_points) _points.Add(p);
        return p;
    }

    public VerletStick CreateStick(VerletPoint a, VerletPoint b, float? length = null)
    {
        var s = new VerletStick(a, b, length ?? Vector2.Distance(a.Position, b.Position));
        lock (_sticks) _sticks.Add(s);
        return s;
    }

    private void PhysicsLoop()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            Update((float)_timeStep);
            Thread.Sleep(16);
        }
    }

    private void Update(float dt)
    {
        lock (_points)
        {
            foreach (var p in _points) 
            {
                p.Acceleration += new Vector2(0, 0.5f); // Gravity
                p.Update(dt);
            }
        }

        lock (_sticks)
        {
            for (int i = 0; i < 3; i++) // Iterations for stability
                foreach (var s in _sticks) s.Update();
        }

        OnUpdate?.Invoke();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _thread.Join(100);
    }
}
