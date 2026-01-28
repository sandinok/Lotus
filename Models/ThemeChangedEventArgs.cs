using Windows.UI;

namespace Lotus.Models;

public class ThemeChangedEventArgs : EventArgs
{
    public string ThemeName { get; }
    public Color PrimaryColor { get; }

    public ThemeChangedEventArgs(string name, Color color)
    {
        ThemeName = name;
        PrimaryColor = color;
    }
}
