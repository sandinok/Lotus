namespace Lotus.Models;

/// <summary>
/// Mascot visual states for animation and appearance
/// </summary>
public enum MascotState
{
    /// <summary>Slow float + calm glow</summary>
    Idle,
    
    /// <summary>Shake + red tint</summary>
    Alert,
    
    /// <summary>Dimmed + subtle particles</summary>
    Sleep,
    
    /// <summary>Bounce + sparkles</summary>
    Happy,
    
    /// <summary>Confetti + colorful glow</summary>
    Party
}
