namespace Lotus.Services;

/// <summary>
/// Time-aware service for greetings and night mode detection
/// </summary>
public sealed class TimeService
{
    private static readonly Lazy<TimeService> _instance = new(() => new TimeService());
    public static TimeService Instance => _instance.Value;

    private TimeService() { }

    /// <summary>
    /// Get time-appropriate greeting for Eli
    /// </summary>
    public string GetGreeting()
    {
        var hour = DateTime.Now.Hour;

        return hour switch
        {
            >= 5 and < 12 => "Good morning, Eli ☀️",
            >= 12 and < 17 => "Good afternoon, Eli 🌤️",
            >= 17 and < 21 => "Good evening, Eli 🌅",
            _ => "Good night, Eli 🌙"
        };
    }

    /// <summary>
    /// Check if it's night time (Aurora mode should activate)
    /// Hour >= 23 OR < 6
    /// </summary>
    public bool IsNightTime()
    {
        var hour = DateTime.Now.Hour;
        return hour >= 23 || hour < 6;
    }

    /// <summary>
    /// Get time of day as enum
    /// </summary>
    public TimeOfDay GetTimeOfDay()
    {
        var hour = DateTime.Now.Hour;

        return hour switch
        {
            >= 5 and < 12 => TimeOfDay.Morning,
            >= 12 and < 17 => TimeOfDay.Afternoon,
            >= 17 and < 21 => TimeOfDay.Evening,
            _ => TimeOfDay.Night
        };
    }
}

public enum TimeOfDay
{
    Morning,
    Afternoon,
    Evening,
    Night
}
