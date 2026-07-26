namespace DesktopOrbit.Models;

public sealed class LauncherItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string DisplayName { get; init; } = string.Empty;
    public string TargetPath { get; init; } = string.Empty;
    public LauncherItemKind Kind { get; init; }
    public string? LaunchArgument { get; init; }
    public string Glyph { get; init; } = "•";
    public bool IsFavorite { get; set; }
    public string Source { get; init; } = "manual";

    public string CategoryLabel => Kind switch
    {
        LauncherItemKind.App => "Application",
        LauncherItemKind.Folder => "Folder",
        _ => "File"
    };

    public string Subtitle => $"{CategoryLabel} • {Source}";
}
