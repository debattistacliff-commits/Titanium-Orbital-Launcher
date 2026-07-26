namespace DesktopOrbit.Models;

public sealed class LauncherSettings
{
    public List<LauncherItem> Favorites { get; set; } = [];
    public string? CustomBackgroundPath { get; set; }
}
