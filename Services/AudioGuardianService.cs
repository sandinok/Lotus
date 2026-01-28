using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Threading.Tasks;

namespace Lotus.Services;

public sealed class AudioGuardianService : IDisposable
{
    private static readonly Lazy<AudioGuardianService> _lazy = new(() => new AudioGuardianService());
    public static AudioGuardianService Instance => _lazy.Value;
    
    private MMDeviceEnumerator? _deviceEnumerator;
    private MMDevice? _device;
    private WasapiLoopbackCapture? _capture;
    
    private volatile bool _isRunning;
    private volatile bool _isDisposed;
    private volatile bool _isProtecting;
    private float _originalVolume = 1f;
    private float _currentVolume = 1f;
    private float _currentRms = 0f;
    private DateTime _lastTick;
    private LimiterState _state = LimiterState.Monitoring;
    private DateTime _releaseStartTime;
    
    private readonly float[] _bands = new float[5];
    private readonly float[] _currentBands = new float[5];
    private readonly object _lock = new();
    
    public event Action<float>? RmsChanged;
    public event Action<float[]>? SpectrumChanged;
    public event Action<string>? StatusChanged;
    public event Action<bool>? ProtectionStateChanged;
    
    public bool IsProtecting => _isProtecting;
    
    private enum LimiterState { Monitoring, Attacking, Holding, Releasing }
    
    private AudioGuardianService() { }
    
    public void Start()
    {
        if (_isDisposed) return;
        if (_isRunning) return;
        
        Task.Run(() =>
        {
            try
            {
                lock (_lock)
                {
                    _deviceEnumerator?.Dispose();
                    _capture?.Dispose();
                    _device?.Dispose();
                    
                    _deviceEnumerator = new MMDeviceEnumerator();
                    _device = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    _originalVolume = _device.AudioEndpointVolume.MasterVolumeLevelScalar;
                    _currentVolume = _originalVolume;
                    
                    _capture = new WasapiLoopbackCapture(_device);
                    _capture.DataAvailable += OnDataAvailable;
                    _capture.RecordingStopped += OnRecordingStopped;
                    _capture.StartRecording();
                    
                    _isRunning = true;
                    _lastTick = DateTime.Now;
                    _state = LimiterState.Monitoring;
                }
                
                StatusChanged?.Invoke("Protecting your ears");
                ProtectionStateChanged?.Invoke(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioGuardian] Start error: {ex.Message}");
                StatusChanged?.Invoke("Audio unavailable");
            }
        });
    }
    
    public void Stop()
    {
        if (!_isRunning) return;
        
        lock (_lock)
        {
            _isRunning = false;
            _isProtecting = false;
            
            try
            {
                _capture?.StopRecording();
            }
            catch { }
            
            try
            {
                if (_device != null && _currentVolume < _originalVolume)
                {
                    _device.AudioEndpointVolume.MasterVolumeLevelScalar = _originalVolume;
                }
            }
            catch { }
            
            _state = LimiterState.Monitoring;
        }
        
        StatusChanged?.Invoke("Guardian paused");
        ProtectionStateChanged?.Invoke(false);
        
        Array.Clear(_bands, 0, _bands.Length);
        Array.Clear(_currentBands, 0, _currentBands.Length);
        _currentRms = 0;
        SpectrumChanged?.Invoke(_currentBands);
        RmsChanged?.Invoke(0);
    }
    
    public float[] GetCurrentBands()
    {
        lock (_lock)
        {
            var result = new float[5];
            Array.Copy(_currentBands, result, 5);
            return result;
        }
    }
    
    public float GetCurrentRms()
    {
        lock (_lock)
        {
            return _currentRms;
        }
    }
    
    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (_isRunning && !_isDisposed)
        {
            Task.Delay(1000).ContinueWith(_ =>
            {
                if (_isRunning && !_isDisposed)
                {
                    _isRunning = false;
                    Start();
                }
            });
        }
    }
    
    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (!_isRunning || e.BytesRecorded == 0) return;
        
        try
        {
            ProcessAudio(e.Buffer, e.BytesRecorded);
        }
        catch { }
    }
    
    private void ProcessAudio(byte[] buffer, int bytesRecorded)
    {
        int sampleCount = bytesRecorded / 4;
        if (sampleCount == 0) return;
        
        var config = ConfigurationService.Instance.Config.Audio;
        float threshold = (float)config.Threshold;
        float safeVolume = (float)config.SafeVolume;
        int recoveryMs = config.RecoveryMs;
        
        // Night mode
        int hour = DateTime.Now.Hour;
        bool isNightMode = hour >= 22 || hour < 6;
        if (isNightMode)
        {
            threshold *= 0.7f;
            safeVolume *= 0.85f;
        }
        
        // Calculate RMS and bands
        float rmsSum = 0;
        float[] bandSums = new float[5];
        int[] bandCounts = new int[5];
        
        float prevSample = 0;
        for (int i = 0; i < sampleCount; i++)
        {
            float sample = BitConverter.ToSingle(buffer, i * 4);
            float abs = Math.Abs(sample);
            rmsSum += sample * sample;
            
            float delta = Math.Abs(sample - prevSample);
            int band = EstimateBand(delta, abs);
            bandSums[band] += abs;
            bandCounts[band]++;
            
            prevSample = sample;
        }
        
        float rms = MathF.Sqrt(rmsSum / sampleCount);
        
        // Normalize and smooth bands
        for (int i = 0; i < 5; i++)
        {
            float avg = bandCounts[i] > 0 ? bandSums[i] / bandCounts[i] : 0;
            if (i == 3) avg *= 1.5f; // Boost high-mids
            _bands[i] = _bands[i] * 0.7f + avg * 0.3f;
        }
        
        // Copy to current for UI
        lock (_lock)
        {
            Array.Copy(_bands, _currentBands, 5);
            _currentRms = rms;
        }
        
        // Check harsh frequencies
        bool harshDetected = _bands[3] > 0.25f || _bands[4] > 0.3f;
        bool shouldLimit = rms > threshold || harshDetected;
        
        ProcessLimiter(shouldLimit, harshDetected, safeVolume, recoveryMs);
        
        RmsChanged?.Invoke(rms);
        SpectrumChanged?.Invoke(_bands);
    }
    
    private int EstimateBand(float delta, float magnitude)
    {
        if (magnitude < 0.01f) return 2;
        float ratio = delta / (magnitude + 0.001f);
        
        if (ratio > 1.2f) return 4;     // Highs
        if (ratio > 0.8f) return 3;     // High-mids (harsh)
        if (ratio > 0.4f) return 2;     // Mids
        if (ratio > 0.15f) return 1;    // Bass
        return 0;                        // Sub-bass
    }
    
    private void ProcessLimiter(bool shouldLimit, bool harsh, float safeVol, int recoveryMs)
    {
        if (_device == null) return;
        
        DateTime now = DateTime.Now;
        float dt = Math.Min((float)(now - _lastTick).TotalSeconds, 0.1f);
        _lastTick = now;
        
        bool wasProtecting = _isProtecting;
        
        switch (_state)
        {
            case LimiterState.Monitoring:
                if (shouldLimit)
                {
                    _state = LimiterState.Attacking;
                    _originalVolume = _device.AudioEndpointVolume.MasterVolumeLevelScalar;
                    _isProtecting = true;
                    StatusChanged?.Invoke(harsh ? "Blocking harsh sound" : "Limiting loud audio");
                }
                break;
                
            case LimiterState.Attacking:
                float attackSpeed = harsh ? 33f : 12f;
                _currentVolume = Lerp(_currentVolume, safeVol, attackSpeed * dt);
                ApplyVolume(_currentVolume);
                
                if (_currentVolume <= safeVol + 0.02f)
                {
                    _state = LimiterState.Holding;
                    _releaseStartTime = now.AddMilliseconds(harsh ? 300 : 800);
                }
                break;
                
            case LimiterState.Holding:
                if (now >= _releaseStartTime)
                {
                    if (!shouldLimit)
                    {
                        _state = LimiterState.Releasing;
                        _releaseStartTime = now;
                        StatusChanged?.Invoke("Releasing...");
                    }
                    else
                    {
                        _releaseStartTime = now.AddMilliseconds(200);
                    }
                }
                break;
                
            case LimiterState.Releasing:
                if (shouldLimit)
                {
                    _state = LimiterState.Attacking;
                    break;
                }
                
                float elapsed = (float)(now - _releaseStartTime).TotalMilliseconds;
                float duration = recoveryMs;
                float t = Math.Clamp(elapsed / duration, 0, 1);
                float eased = 1 - MathF.Pow(1 - t, 3);
                
                _currentVolume = Lerp(safeVol, _originalVolume, eased);
                ApplyVolume(_currentVolume);
                
                if (t >= 1f)
                {
                    _currentVolume = _originalVolume;
                    ApplyVolume(_currentVolume);
                    _state = LimiterState.Monitoring;
                    _isProtecting = false;
                    StatusChanged?.Invoke("Protecting your ears");
                }
                break;
        }
        
        if (wasProtecting != _isProtecting)
        {
            ProtectionStateChanged?.Invoke(_isProtecting);
        }
    }
    
    private void ApplyVolume(float vol)
    {
        try
        {
            if (_device != null)
                _device.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(vol, 0.05f, 1f);
        }
        catch { }
    }
    
    private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0, 1);
    
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        
        Stop();
        
        lock (_lock)
        {
            _capture?.Dispose();
            _device?.Dispose();
            _deviceEnumerator?.Dispose();
        }
    }
}