using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Numerics;
using Windows.Graphics;
using Windows.UI;
using WinRT.Interop;
using Lotus.Services;
using Lotus.Models;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Lotus;

public sealed partial class MainWindow : Window
{
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _spectrumTimer;
    private readonly DispatcherTimer _partyTimer;
    private int _mascotClickCount = 0;
    private DateTime _lastSecretMessageTime = DateTime.MinValue;
    private bool _isAudioActive = true;
    private MascotState _currentMascotState = MascotState.Idle;
    private readonly Microsoft.UI.Composition.Compositor _compositor;
    private Microsoft.UI.Composition.Vector3KeyFrameAnimation? _floatAnimation;
    private Microsoft.UI.Composition.ScalarKeyFrameAnimation? _glowAnimation;

    public MainWindow()
    {
        this.InitializeComponent();
        
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(HeaderBorder);
        ConfigureWindow();

        _compositor = this.Compositor;

        // Timers
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (s, e) => UpdateTime();
        _clockTimer.Start();

        _spectrumTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) }; // 30fps
        _spectrumTimer.Tick += SpectrumTimer_Tick;

        _partyTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _partyTimer.Tick += (s, e) => { if (_currentMascotState == MascotState.Party) SetMascotState(MascotState.Idle); };

        // Events
        this.Closed += MainWindow_Closed;
        this.Activated += MainWindow_Activated;

        InitializeAll();
    }

    private void InitializeAll()
    {
        InitializeTheme();
        InitializeMascotAnimations();
        LoadContent();
        SetupAudioGuardian();
        UpdateTime();
    }

    private void ConfigureWindow()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        
        appWindow.Resize(new SizeInt32(420, 720));
        appWindow.Title = "LOTUS";
        
        var display = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        if (display != null)
        {
            appWindow.Move(new PointInt32(
                (display.WorkArea.Width - 420) / 2, 
                (display.WorkArea.Height - 720) / 2));
        }

        var presenter = appWindow.Presenter as OverlappedPresenter;
        presenter?.SetBorderAndTitleBar(true, false);
    }

    private void InitializeTheme()
    {
        // Force initial brush load
        var brush = ThemeService.Instance.GetCurrentPrimaryBrush();
        MascotBody.Fill = brush;
        MascotHead.Fill = brush;
    }

    private void InitializeMascotAnimations()
    {
        // Entry animation
        var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(MascotContainer);
        visual.Opacity = 0;
        visual.Offset = new Vector3(0, 20, 0);
        
        var entryAnim = _compositor.CreateVector3KeyFrameAnimation();
        entryAnim.InsertKeyFrame(0, new Vector3(0, 20, 0));
        entryAnim.InsertKeyFrame(1, new Vector3(0, 0, 0));
        entryAnim.Duration = TimeSpan.FromMilliseconds(800);
        entryAnim.DelayTime = TimeSpan.FromMilliseconds(200);
        
        var fadeAnim = _compositor.CreateScalarKeyFrameAnimation();
        fadeAnim.InsertKeyFrame(0, 0);
        fadeAnim.InsertKeyFrame(1, 1);
        fadeAnim.Duration = TimeSpan.FromMilliseconds(600);
        fadeAnim.DelayTime = TimeSpan.FromMilliseconds(200);
        
        visual.StartAnimation("Offset", entryAnim);
        visual.StartAnimation("Opacity", fadeAnim);

        // Float loop
        _floatAnimation = _compositor.CreateVector3KeyFrameAnimation();
        _floatAnimation.InsertKeyFrame(0, new Vector3(0, 0, 0));
        _floatAnimation.InsertKeyFrame(0.5f, new Vector3(0, -6, 0));
        _floatAnimation.InsertKeyFrame(1, new Vector3(0, 0, 0));
        _floatAnimation.Duration = TimeSpan.FromSeconds(4);
        _floatAnimation.IterationBehavior = Microsoft.UI.Composition.AnimationIterationBehavior.Forever;
        
        // Start float after entry
        DispatcherQueue.TryEnqueue(async () =>
        {
            await System.Threading.Tasks.Task.Delay(1000);
            if (_currentMascotState != MascotState.Alert)
                visual.StartAnimation("Offset", _floatAnimation);
        });

        // Glow pulse
        var glowVisual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(MascotGlow);
        _glowAnimation = _compositor.CreateScalarKeyFrameAnimation();
        _glowAnimation.InsertKeyFrame(0, 0.2f);
        _glowAnimation.InsertKeyFrame(0.5f, 0.35f);
        _glowAnimation.InsertKeyFrame(1, 0.2f);
        _glowAnimation.Duration = TimeSpan.FromSeconds(3);
        _glowAnimation.IterationBehavior = Microsoft.UI.Composition.AnimationIterationBehavior.Forever;
        glowVisual.StartAnimation("Opacity", _glowAnimation);

        // Brain events
        MascotBrain.Instance.RequestAnimation += (s, anim) => 
        {
            DispatcherQueue.TryEnqueue(() => PlayMascotAnimation(anim));
        };
        
        MascotBrain.Instance.RequestThought += (s, thought) =>
        {
            DispatcherQueue.TryEnqueue(() => ShowMascotThought(thought));
        };
    }

    private void LoadContent()
    {
        var config = ConfigurationService.Instance.Config;
        
        DailyMessageText.Text = ConfigurationService.Instance.GetRandomDailyMessage();
        HimNoteText.Text = string.IsNullOrEmpty(config.HimNote) ? "No note yet..." : config.HimNote;
        EliNoteTextBox.Text = config.EliNote ?? "";
        
        var currentTheme = config.CurrentTheme;
        for (int i = 0; i < ThemeSelector.Items.Count; i++)
        {
            if (ThemeSelector.Items[i] is ComboBoxItem item && item.Tag?.ToString() == currentTheme)
            {
                ThemeSelector.SelectedIndex = i;
                break;
            }
        }
    }

    private void SetupAudioGuardian()
    {
        AudioGuardianService.Instance.StatusChanged += (status) =>
        {
            DispatcherQueue.TryEnqueue(() => AudioStatusText.Text = status);
        };
        
        AudioGuardianService.Instance.ProtectionStateChanged += (active) =>
        {
            DispatcherQueue.TryEnqueue(() => 
            {
                if (active)
                {
                    SetMascotState(MascotState.Alert);
                    AudioStatusText.Foreground = new SolidColorBrush(Colors.OrangeRed);
                }
                else
                {
                    if (_currentMascotState == MascotState.Alert)
                        SetMascotState(MascotState.Idle);
                    AudioStatusText.Foreground = (SolidColorBrush)Application.Current.Resources["AeroCaptionStyle"];
                }
            });
        };

        if (_isAudioActive)
        {
            AudioGuardianService.Instance.Start();
            _spectrumTimer.Start();
        }
    }

    private void SpectrumTimer_Tick(object? sender, object e)
    {
        if (!_isAudioActive) return;
        
        try
        {
            var bands = AudioGuardianService.Instance.GetCurrentBands();
            bool isProtecting = AudioGuardianService.Instance.IsProtecting;
            
            // Apply values with smoothing
            Bar1.Value = Math.Min(1, bands[0] * 2.5);
            Bar2.Value = Math.Min(1, bands[1] * 2.5);
            Bar3.Value = Math.Min(1, bands[2] * 2.5);
            Bar4.Value = Math.Min(1, bands[3] * 2.5);
            Bar5.Value = Math.Min(1, bands[4] * 2.5);
            
            // Color logic for harsh frequencies (bands 3 and 4)
            if (isProtecting)
            {
                var warningBrush = (LinearGradientBrush)Application.Current.Resources["AeroLiquidBarWarning"];
                var dangerBrush = (LinearGradientBrush)Application.Current.Resources["AeroLiquidBarDanger"];
                
                // If high bands are high, show warning colors
                if (bands[3] > 0.3 || bands[4] > 0.3)
                {
                    Bar4.Background = warningBrush;
                    Bar5.Background = dangerBrush;
                }
                else
                {
                    Bar4.Background = warningBrush;
                    Bar5.Background = warningBrush;
                }
            }
            else
            {
                // Reset to theme gradient
                var normalBrush = (LinearGradientBrush)Application.Current.Resources["AeroLiquidBarGradient"];
                Bar4.Background = normalBrush;
                Bar5.Background = normalBrush;
            }
        }
        catch { }
    }

    private void UpdateTime()
    {
        var greeting = TimeService.Instance.GetGreeting();
        // Optional: show in status or title
    }

    private void SetMascotState(MascotState state)
    {
        if (_currentMascotState == state) return;
        _currentMascotState = state;

        var brush = ThemeService.Instance.GetCurrentPrimaryBrush();
        var alertBrush = new SolidColorBrush(Colors.Red);
        var sleepBrush = new SolidColorBrush(Colors.SlateGray);
        var happyBrush = new SolidColorBrush(Colors.HotPink);
        var partyBrush = new SolidColorBrush(Colors.Gold);

        var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(MascotContainer);
        visual.StopAnimation("Offset");

        switch (state)
        {
            case MascotState.Idle:
                MascotBody.Fill = brush;
                MascotHead.Fill = brush;
                MascotGlow.Fill = brush;
                MascotGlow.Opacity = 0.3;
                MascotStatusText.Text = " idle...";
                LeftEye.Height = 8;
                RightEye.Height = 8;
                MascotExpression.Text = "";
                visual.StartAnimation("Offset", _floatAnimation);
                _partyTimer.Stop();
                break;
                
            case MascotState.Alert:
                MascotBody.Fill = alertBrush;
                MascotHead.Fill = alertBrush;
                MascotGlow.Fill = alertBrush;
                MascotGlow.Opacity = 0.5;
                MascotStatusText.Text = "! loud noise detected";
                LeftEye.Height = 8;
                RightEye.Height = 8;
                MascotExpression.Text = "!!";
                
                // Shake
                var shake = _compositor.CreateVector3KeyFrameAnimation();
                shake.InsertKeyFrame(0, new Vector3(0, 0, 0));
                shake.InsertKeyFrame(0.1f, new Vector3(-4, 0, 0));
                shake.InsertKeyFrame(0.2f, new Vector3(4, 0, 0));
                shake.InsertKeyFrame(0.3f, new Vector3(-4, 0, 0));
                shake.InsertKeyFrame(0.4f, new Vector3(4, 0, 0));
                shake.InsertKeyFrame(0.5f, new Vector3(0, 0, 0));
                shake.Duration = TimeSpan.FromMilliseconds(500);
                visual.StartAnimation("Offset", shake);
                _partyTimer.Stop();
                break;
                
            case MascotState.Sleep:
                MascotBody.Fill = sleepBrush;
                MascotHead.Fill = sleepBrush;
                MascotGlow.Fill = sleepBrush;
                MascotGlow.Opacity = 0.15;
                LeftEye.Height = 2;
                RightEye.Height = 2;
                MascotExpression.Text = "zZ";
                MascotStatusText.Text = " zzz...";
                _partyTimer.Stop();
                break;
                
            case MascotState.Happy:
                MascotBody.Fill = happyBrush;
                MascotHead.Fill = happyBrush;
                MascotGlow.Fill = happyBrush;
                MascotGlow.Opacity = 0.4;
                LeftEye.Height = 8;
                RightEye.Height = 8;
                MascotExpression.Text = "Love";
                MascotStatusText.Text = " happy!";
                
                var bounce = _compositor.CreateVector3KeyFrameAnimation();
                bounce.InsertKeyFrame(0, new Vector3(0, 0, 0));
                bounce.InsertKeyFrame(0.3f, new Vector3(0, -15, 0));
                bounce.InsertKeyFrame(0.5f, new Vector3(0, 0, 0));
                bounce.Duration = TimeSpan.FromMilliseconds(500);
                visual.StartAnimation("Offset", bounce);
                
                // Return to idle after 3s
                DispatcherQueue.TryEnqueue(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(3000);
                    if (_currentMascotState == MascotState.Happy)
                        SetMascotState(MascotState.Idle);
                });
                _partyTimer.Stop();
                break;
                
            case MascotState.Party:
                MascotBody.Fill = partyBrush;
                MascotHead.Fill = partyBrush;
                MascotGlow.Fill = partyBrush;
                MascotGlow.Opacity = 0.6;
                LeftEye.Height = 8;
                RightEye.Height = 8;
                MascotExpression.Text = "Star";
                MascotStatusText.Text = " party mode!";
                
                var partyAnim = _compositor.CreateVector3KeyFrameAnimation();
                partyAnim.InsertKeyFrame(0, new Vector3(0, 0, 0));
                partyAnim.InsertKeyFrame(0.25f, new Vector3(0, -10, 0));
                partyAnim.InsertKeyFrame(0.5f, new Vector3(0, 0, 0));
                partyAnim.InsertKeyFrame(0.75f, new Vector3(0, -5, 0));
                partyAnim.InsertKeyFrame(1, new Vector3(0, 0, 0));
                partyAnim.Duration = TimeSpan.FromSeconds(1);
                partyAnim.IterationBehavior = Microsoft.UI.Composition.AnimationIterationBehavior.Forever;
                visual.StartAnimation("Offset", partyAnim);
                
                _partyTimer.Start();
                break;
        }
    }

    private void PlayMascotAnimation(string animationName)
    {
        switch (animationName)
        {
            case "Stretch":
                SetMascotState(MascotState.Happy);
                ShowMascotThought("Time to stretch!");
                break;
            case "Stargaze":
                SetMascotState(MascotState.Sleep);
                break;
            case "Yawn":
                SetMascotState(MascotState.Sleep);
                break;
            case "BreathingStart":
                MascotStatusText.Text = " breathe with me...";
                MascotExpression.Text = "○";
                break;
            case "BreathingEnd":
                SetMascotState(MascotState.Happy);
                break;
        }
    }

    private void ShowMascotThought(string thought)
    {
        MascotStatusText.Text = $" {thought}";
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState != WindowActivationState.Deactivated)
        {
            if (_isAudioActive) AudioGuardianService.Instance.Start();
            if (_currentMascotState == MascotState.Sleep) SetMascotState(MascotState.Idle);
        }
        else
        {
            AudioGuardianService.Instance.Stop();
        }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _clockTimer?.Stop();
        _spectrumTimer?.Stop();
        _partyTimer?.Stop();
        AudioGuardianService.Instance.Dispose();
        MascotBrain.Instance.Dispose();
    }

    private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeSelector.SelectedItem is ComboBoxItem item && item.Tag is string themeKey)
        {
            ThemeService.Instance.SetTheme(themeKey);
            SetMascotState(_currentMascotState); // Refresh colors
            DailyMessageText.Text = ConfigurationService.Instance.GetRandomDailyMessage(); // New message on theme change
        }
    }

    private void PowerToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton toggle)
        {
            _isAudioActive = toggle.IsChecked ?? false;
            toggle.Content = _isAudioActive ? "ON" : "OFF";
            
            if (_isAudioActive)
            {
                AudioGuardianService.Instance.Start();
                _spectrumTimer.Start();
                AudioStatusText.Text = "Protecting your ears";
            }
            else
            {
                AudioGuardianService.Instance.Stop();
                _spectrumTimer.Stop();
                AudioStatusText.Text = "Guardian paused";
                Bar1.Value = Bar2.Value = Bar3.Value = Bar4.Value = Bar5.Value = 0;
                if (_currentMascotState == MascotState.Alert) SetMascotState(MascotState.Idle);
            }
        }
    }

    private void EliNoteTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ConfigurationService.Instance.UpdateEliNote(EliNoteTextBox.Text);
    }

    private void ClearNoteButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(EliNoteTextBox.Text))
        {
            EliNoteTextBox.Text = "";
            SetMascotState(MascotState.Party);
            ShowToast("Cleared! *");
        }
    }

    private void MascotContainer_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _mascotClickCount++;
        
        var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(MascotContainer);
        var bounce = _compositor.CreateVector3KeyFrameAnimation();
        bounce.InsertKeyFrame(0, new Vector3(0, 0, 0));
        bounce.InsertKeyFrame(0.2f, new Vector3(0, -12, 0));
        bounce.InsertKeyFrame(0.4f, new Vector3(0, 0, 0));
        bounce.Duration = TimeSpan.FromMilliseconds(200);
        visual.StartAnimation("Offset", bounce);

        if (_mascotClickCount >= 5)
        {
            _mascotClickCount = 0;
            SetMascotState(MascotState.Happy);
            ShowSecretMessagePopup("Made with love for Eli <3");
        }
    }

    private void MascotContainer_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        // MascotContainer.Cursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);
        if (_currentMascotState != MascotState.Alert && _currentMascotState != MascotState.Party)
        {
            var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(MascotContainer);
            var scale = _compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0, new Vector3(1, 1, 1));
            scale.InsertKeyFrame(1, new Vector3(1.08f, 1.08f, 1));
            scale.Duration = TimeSpan.FromMilliseconds(200);
            visual.StartAnimation("Scale", scale);
        }
    }

    private void MascotContainer_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (_currentMascotState != MascotState.Alert && _currentMascotState != MascotState.Party)
        {
            var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(MascotContainer);
            var scale = _compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0, new Vector3(1.08f, 1.08f, 1));
            scale.InsertKeyFrame(1, new Vector3(1, 1, 1));
            scale.Duration = TimeSpan.FromMilliseconds(200);
            visual.StartAnimation("Scale", scale);
        }
    }

    private void SecretArea_Click(object sender, RoutedEventArgs e)
    {
        if ((DateTime.Now - _lastSecretMessageTime).TotalSeconds < 10) return;

        _lastSecretMessageTime = DateTime.Now;
        var message = ConfigurationService.Instance.GetRandomSecretMessage();
        ShowSecretMessagePopup(message);
    }

    private void ShowSecretMessagePopup(string message)
    {
        SecretMessageText.Text = message;
        SecretMessagePopup.Visibility = Visibility.Visible;
        
        var popupVisual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(SecretMessagePopup);
        
        var fade = _compositor.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0, 0);
        fade.InsertKeyFrame(1, 1);
        fade.Duration = TimeSpan.FromMilliseconds(300);
        popupVisual.StartAnimation("Opacity", fade);

        DispatcherQueue.TryEnqueue(async () =>
        {
            await System.Threading.Tasks.Task.Delay(3000);
            
            if (SecretMessagePopup.Visibility == Visibility.Visible)
            {
                var fadeOut = _compositor.CreateScalarKeyFrameAnimation();
                fadeOut.InsertKeyFrame(0, 1);
                fadeOut.InsertKeyFrame(1, 0);
                fadeOut.Duration = TimeSpan.FromMilliseconds(300);
                popupVisual.StartAnimation("Opacity", fadeOut);
                
                await System.Threading.Tasks.Task.Delay(300);
                SecretMessagePopup.Visibility = Visibility.Collapsed;
            }
        });
    }

    private void ShowToast(string message)
    {
        MascotStatusText.Text = message;
        DispatcherQueue.TryEnqueue(async () =>
        {
            await System.Threading.Tasks.Task.Delay(2000);
            if (MascotStatusText.Text == message && _currentMascotState == MascotState.Idle)
                MascotStatusText.Text = " idle...";
        });
    }
}