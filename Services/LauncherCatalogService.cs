using System.Diagnostics;
using System.IO;
using System.Text.Json;
using DesktopOrbit.Models;

namespace DesktopOrbit.Services;

public sealed class LauncherCatalogService
{
    private static readonly string[] ShortcutRoots =
    [
        Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
    ];

    private static readonly string[] SearchableExtensions = [".lnk", ".url", ".exe", ".bat", ".cmd", ".ps1", ".txt", ".md", ".pdf", ".docx", ".xlsx", ".png", ".jpg", ".mp4", ".mp3"];
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "$Recycle.Bin",
        "System Volume Information",
        "Windows",
        "Program Files",
        "Program Files (x86)",
        "ProgramData"
    };

    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private readonly string _settingsPath;
    private readonly string _settingsDirectory;

    public LauncherCatalogService()
    {
        _settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopOrbit");
        _settingsPath = Path.Combine(_settingsDirectory, "settings.json");
    }

    public async Task<LauncherSettings> LoadSettingsAsync()
    {
        Directory.CreateDirectory(_settingsDirectory);

        if (!File.Exists(_settingsPath))
        {
            return CreateDefaultSettings();
        }

        await using var stream = File.OpenRead(_settingsPath);
        var settings = await JsonSerializer.DeserializeAsync<LauncherSettings>(stream) ?? new LauncherSettings();
        settings.Favorites = settings.Favorites
            .Where(item => !string.IsNullOrWhiteSpace(item.TargetPath))
            .ToList();
        return settings;
    }

    public async Task SaveSettingsAsync(LauncherSettings settings)
    {
        Directory.CreateDirectory(_settingsDirectory);
        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, _jsonOptions);
    }

    public async Task<IReadOnlyList<LauncherItem>> BuildIndexAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var items = new Dictionary<string, LauncherItem>(StringComparer.OrdinalIgnoreCase);
        AddShortcutItems(items, progress, cancellationToken);
        await AddDriveItemsAsync(items, progress, cancellationToken);
        progress?.Report($"Indexed {items.Count:N0} items");
        return items.Values.OrderBy(item => item.DisplayName).ToList();
    }

    public LauncherItem CreateItemFromPath(string path, LauncherItemKind preferredKind, string source)
    {
        var isDirectory = Directory.Exists(path);
        var isFile = File.Exists(path);
        var resolvedKind = preferredKind;

        if (isDirectory)
        {
            resolvedKind = LauncherItemKind.Folder;
        }
        else if (isFile && preferredKind != LauncherItemKind.App)
        {
            resolvedKind = LauncherItemKind.File;
        }

        return new LauncherItem
        {
            DisplayName = Path.GetFileNameWithoutExtension(path) switch
            {
                "" => Path.GetFileName(path),
                var value => value
            },
            TargetPath = path,
            Kind = resolvedKind,
            Glyph = GetGlyph(resolvedKind, path),
            Source = source,
            IsFavorite = true
        };
    }

    public async Task<IReadOnlyList<LauncherItem>> ImportDesktopAsync()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var items = new List<LauncherItem>();

        foreach (var entry in Directory.EnumerateFileSystemEntries(desktop))
        {
            var kind = Directory.Exists(entry)
                ? LauncherItemKind.Folder
                : IsAppPath(entry) ? LauncherItemKind.App : LauncherItemKind.File;
            items.Add(CreateItemFromPath(entry, kind, "desktop"));
        }

        return await Task.FromResult(items);
    }

    public async Task<IReadOnlyList<LauncherItem>> OrganizeDesktopAsync()
    {
        var libraryRoot = GetLibraryRoot();
        var shortcutsRoot = GetShortcutsRoot();
        var foldersRoot = GetFoldersRoot();
        var filesRoot = GetFilesRoot();

        Directory.CreateDirectory(libraryRoot);
        Directory.CreateDirectory(shortcutsRoot);
        Directory.CreateDirectory(foldersRoot);
        Directory.CreateDirectory(filesRoot);

        var movedItems = new List<LauncherItem>();
        foreach (var desktop in GetDesktopSources())
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(desktop))
            {
                var name = Path.GetFileName(entry);
                if (name.Equals("Desktop Orbit.lnk", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var destinationRoot = Directory.Exists(entry)
                    ? foldersRoot
                    : IsAppPath(entry) ? shortcutsRoot : filesRoot;
                var destination = GetUniqueDestination(Path.Combine(destinationRoot, name));

                if (Directory.Exists(entry))
                {
                    Directory.Move(entry, destination);
                    movedItems.Add(CreateItemFromPath(destination, LauncherItemKind.Folder, "desktop-organized"));
                    continue;
                }

                File.Move(entry, destination);
                var kind = IsAppPath(destination) ? LauncherItemKind.App : LauncherItemKind.File;
                movedItems.Add(CreateItemFromPath(destination, kind, "desktop-organized"));
            }
        }

        movedItems.Insert(0, CreateItemFromPath(shortcutsRoot, LauncherItemKind.Folder, "desktop-organized"));
        movedItems.Insert(1, CreateItemFromPath(foldersRoot, LauncherItemKind.Folder, "desktop-organized"));
        movedItems.Insert(2, CreateItemFromPath(filesRoot, LauncherItemKind.Folder, "desktop-organized"));

        return await Task.FromResult<IReadOnlyList<LauncherItem>>(movedItems);
    }

    public string GetLibraryRoot() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Desktop Orbit Library");

    public string GetShortcutsRoot() => Path.Combine(GetLibraryRoot(), "Shortcuts");

    public string GetFoldersRoot() => Path.Combine(GetLibraryRoot(), "Folders");

    public string GetFilesRoot() => Path.Combine(GetLibraryRoot(), "Files");

    public IReadOnlyList<string> GetDesktopSources()
    {
        var candidates = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory))
        };

        return candidates
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(Directory.Exists)
            .ToList();
    }

    public async Task<ManagedLibrarySnapshot> LoadManagedLibraryAsync()
    {
        var snapshot = new ManagedLibrarySnapshot
        {
            LibraryRoot = GetLibraryRoot(),
            ShortcutsRoot = GetShortcutsRoot(),
            FoldersRoot = GetFoldersRoot(),
            FilesRoot = GetFilesRoot(),
            Shortcuts = LoadDirectItems(GetShortcutsRoot(), LauncherItemKind.App, "managed-shortcuts"),
            Folders = LoadDirectItems(GetFoldersRoot(), LauncherItemKind.Folder, "managed-folders"),
            Files = LoadDirectItems(GetFilesRoot(), LauncherItemKind.File, "managed-files")
        };

        return await Task.FromResult(snapshot);
    }

    public static void Open(LauncherItem item)
    {
        if (string.IsNullOrWhiteSpace(item.TargetPath))
        {
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = item.TargetPath,
            UseShellExecute = true
        };

        if (!string.IsNullOrWhiteSpace(item.LaunchArgument))
        {
            startInfo.Arguments = item.LaunchArgument;
        }

        Process.Start(startInfo);
    }

    private LauncherSettings CreateDefaultSettings()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        return new LauncherSettings
        {
            Favorites =
            [
                CreateItemFromPath(desktop, LauncherItemKind.Folder, "default"),
                CreateItemFromPath(documents, LauncherItemKind.Folder, "default"),
                CreateItemFromPath(downloads, LauncherItemKind.Folder, "default")
            ]
        };
    }

    private static void AddShortcutItems(IDictionary<string, LauncherItem> items, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        foreach (var root in ShortcutRoots.Where(Directory.Exists))
        {
            progress?.Report($"Scanning launcher shortcuts in {root}");
            foreach (var file in EnumerateSafe(root))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsAppPath(file))
                {
                    continue;
                }

                items.TryAdd(file, new LauncherItem
                {
                    DisplayName = Path.GetFileNameWithoutExtension(file),
                    TargetPath = file,
                    Kind = LauncherItemKind.App,
                    Glyph = GetGlyph(LauncherItemKind.App, file),
                    Source = "start-menu"
                });
            }
        }
    }

    private static async Task AddDriveItemsAsync(IDictionary<string, LauncherItem> items, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var fixedDrives = DriveInfo.GetDrives()
            .Where(drive => drive.IsReady && drive.DriveType == DriveType.Fixed)
            .ToList();

        foreach (var drive in fixedDrives)
        {
            progress?.Report($"Scanning {drive.Name}");
            foreach (var path in EnumerateSafe(drive.RootDirectory.FullName))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!IsSearchable(path))
                {
                    continue;
                }

                var kind = Directory.Exists(path)
                    ? LauncherItemKind.Folder
                    : IsAppPath(path) ? LauncherItemKind.App : LauncherItemKind.File;
                items.TryAdd(path, new LauncherItem
                {
                    DisplayName = Path.GetFileNameWithoutExtension(path) switch
                    {
                        "" => Path.GetFileName(path),
                        var value => value
                    },
                    TargetPath = path,
                    Kind = kind,
                    Glyph = GetGlyph(kind, path),
                    Source = drive.Name.TrimEnd('\\')
                });

                if (items.Count % 400 == 0)
                {
                    progress?.Report($"Indexed {items.Count:N0} items...");
                    await Task.Yield();
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateSafe(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            IEnumerable<string> directories = [];
            IEnumerable<string> files = [];

            try
            {
                directories = Directory.EnumerateDirectories(current);
            }
            catch
            {
            }

            foreach (var directory in directories)
            {
                if (ExcludedDirectories.Contains(Path.GetFileName(directory)))
                {
                    continue;
                }

                yield return directory;
                pending.Push(directory);
            }

            try
            {
                files = Directory.EnumerateFiles(current);
            }
            catch
            {
            }

            foreach (var file in files)
            {
                yield return file;
            }
        }
    }

    private static bool IsSearchable(string path)
    {
        if (Directory.Exists(path))
        {
            return true;
        }

        var extension = Path.GetExtension(path);
        return SearchableExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsAppPath(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".url", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".bat", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetGlyph(LauncherItemKind kind, string path) => kind switch
    {
        LauncherItemKind.App => "◉",
        LauncherItemKind.Folder => "▣",
        _ => Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase) ? "◫" : "•"
    };

    private List<LauncherItem> LoadDirectItems(string root, LauncherItemKind defaultKind, string source)
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        return Directory.EnumerateFileSystemEntries(root)
            .Select(path =>
            {
                var kind = Directory.Exists(path)
                    ? LauncherItemKind.Folder
                    : defaultKind == LauncherItemKind.App && IsAppPath(path) ? LauncherItemKind.App : defaultKind;
                return CreateItemFromPath(path, kind, source);
            })
            .OrderBy(item => item.DisplayName)
            .ToList();
    }

    private static string GetUniqueDestination(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        var counter = 2;

        while (true)
        {
            var candidate = Path.Combine(directory, $"{fileNameWithoutExtension} ({counter}){extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }

            counter++;
        }
    }
}
