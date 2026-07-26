namespace DesktopOrbit.Models;

public sealed class ManagedLibrarySnapshot
{
    public string LibraryRoot { get; init; } = string.Empty;
    public string ShortcutsRoot { get; init; } = string.Empty;
    public string FoldersRoot { get; init; } = string.Empty;
    public string FilesRoot { get; init; } = string.Empty;
    public IReadOnlyList<LauncherItem> Shortcuts { get; init; } = [];
    public IReadOnlyList<LauncherItem> Folders { get; init; } = [];
    public IReadOnlyList<LauncherItem> Files { get; init; } = [];
}
