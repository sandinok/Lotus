// Services/AudioGuardian.cs
using System;
using System.Linq;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Avalonia.Threading;

namespace Lotus.Services;

public sealed class AudioGuardian : IDisposable
{
    private readonly WasapiLoopbackCapture? _capture;
    private readonly MMDevice? _device;
    private readonly float[] _buffer = new float[2048];
    private readonly double[] _bands = new double[5];
    private readonly Timer _limiterTimer;
    private DateTime _lastAlert;
    private bool _limiterActive;
    
    public double[] Bands => _bands;
    public bool IsLimiterActive => _limiterActive;
    public event Action<double[]>? OnSpectrumUpdate;
    public event Action? OnLimitTriggered;

    public AudioGuardian()
    {
        try
        {
            var enumerator = new MMDeviceEnumerator();
            _device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            if (_device?.AudioMeterInformation != null)
            {
                SetupMeterMode();
            }
            else
            {
                _capture = new WasapiLoopbackCapture(_device);
                _capture.DataAvailable += OnDataAvailable;
                _capture.StartRecording();
            }
        }
        catch
        {
            // Fallback to null implementation
        }
        
        _limiterTimer = new Timer(_ => CheckLimiter(), null, 100, 100);
    }

    private void SetupMeterMode()
    {
        new Thread(() =>
        {
            while (true)
            {
                try
                {
                    var peak = _device!.AudioMeterInformation.MasterPeakValue;
                    _bands[2] = peak;
                    Dispatcher.UIThread.Post(() => OnSpectrumUpdate?.Invoke(_bands));
                }
                catch { }
                Thread.Sleep(50);
            }
        }) { IsBackground = true }.Start();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var samples = e.BytesRecorded / 4;
        if (samples > _buffer.Length) return;
        
        Buffer.BlockCopy(e.Buffer, 0, _buffer, 0, e.BytesRecorded);
        
        var bandSize = samples / 5;
        for (int b = 0; b < 5; b++)
        {
            double sum = 0;
            int start = b * bandSize;
            int end = start + bandSize;
            for (int i = start; i < end && i < samples; i++)
                sum += _buffer[i] * _buffer[i];
            _bands[b] = Math.Sqrt(sum / bandSize);
        }
        
        Dispatcher.UIThread.Post(() => OnSpectrumUpdate?.Invoke(_bands));
    }

    private void CheckLimiter()
    {
        var threshold = 0.6;
        var max = _bands.Max();
        
        if (max > threshold && !_limiterActive)
        {
            _limiterActive = true;
            _lastAlert = DateTime.Now;
            Dispatcher.UIThread.Post(() => OnLimitTriggered?.Invoke());
            _limiterTimer.Change(3000, 100);
        }
        else if (_limiterActive && (DateTime.Now - _lastAlert).TotalMilliseconds > 3000)
        {
            _limiterActive = false;
        }
    }

    public void Dispose()
    {
        _capture?.StopRecording();
        _capture?.Dispose();
        _limiterTimer.Dispose();
        _device?.Dispose();
    }
}
