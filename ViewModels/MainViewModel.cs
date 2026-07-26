using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DesktopOrbit.Models;
using DesktopOrbit.Services;
using System.Windows.Threading;
using System.Net.Http.Json;
using System.Net.Http;

namespace DesktopOrbit.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private const int OrbitPageSize = 10;
    private static readonly System.Windows.Media.Brush[] OrbitBrushes =
    [
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(67, 118, 255)),
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(79, 196, 172)),
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(233, 145, 90)),
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(168, 112, 255)),
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(78, 161, 220)),
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(225, 112, 145)),
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(109, 205, 232)),
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(136, 201, 88))
    ];

    private readonly LauncherCatalogService _catalogService = new();
    private readonly SystemTelemetryService _telemetryService = new();
    private readonly ObservableCollection<LauncherItem> _catalog = [];
    private CancellationTokenSource? _refreshCancellationSource;
    private string _searchText = string.Empty;
    private string _statusText = "Ready";
    private string _orbitSummaryTitle = "Orbit";
    private string _orbitSummaryText = "Pin your most-used apps and folders, then search the rest of the machine from one place.";
    private int _orbitPageIndex;
    private string _currentTimeText = DateTime.Now.ToString("HH:mm:ss");
    private string _currentDateText = DateTime.Now.ToString("dd/MM/yyyy");
    private string _currentFullDateText = string.Empty;
    private LauncherSettings _settings = new();
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _telemetryTimer;
    private double _cpuUsage;
    private double _ramUsage;
    private double _diskUsage;
    private string _ramSummary = "Reading memory...";
    private string _diskSummary = "Reading system drive...";
    private string _systemUptimeText = "UP 00:00";
    private static readonly HttpClient RadioClient = new()
    {
        BaseAddress = new Uri("https://de1.api.radio-browser.info/"),
        Timeout = TimeSpan.FromSeconds(12)
    };
    private string _radioSearchText = string.Empty;
    private string _radioStatusText = "Worldwide stations ready";
    private bool _isRadioMode;
    private string _radioRegion = "WORLD";
    private string _selectedOrbitName = "READY";
    private string _selectedOrbitSubtitle = "Rotate the dial";
    private double _orbitRotationDegrees;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<LauncherItem> Favorites { get; } = [];
    public ObservableCollection<LauncherItem> SearchResults { get; } = [];
    public ObservableCollection<OrbitNode> OrbitItems { get; } = [];
    public ObservableCollection<LauncherItem> ManagedShortcuts { get; } = [];
    public ObservableCollection<LauncherItem> ManagedFolders { get; } = [];
    public ObservableCollection<LauncherItem> ManagedFiles { get; } = [];
    public ObservableCollection<LauncherItem> RecentShortcuts { get; } = [];
    public ObservableCollection<RadioStation> RadioStations { get; } = [];
    public string? CustomBackgroundPath => _settings.CustomBackgroundPath;

    public MainViewModel()
    {
        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();

        _telemetryTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _telemetryTimer.Tick += (_, _) => UpdateTelemetry();
        _telemetryTimer.Start();
        UpdateClock();
        UpdateTelemetry();
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                UpdateSearchResults();
            }
        }
    }

    public string RadioSearchText
    {
        get => _radioSearchText;
        set => SetProperty(ref _radioSearchText, value);
    }

    public string RadioStatusText
    {
        get => _radioStatusText;
        private set => SetProperty(ref _radioStatusText, value);
    }

    public bool IsRadioMode
    {
        get => _isRadioMode;
        private set => SetProperty(ref _isRadioMode, value);
    }

    public string RadioRegion
    {
        get => _radioRegion;
        private set => SetProperty(ref _radioRegion, value);
    }

    public string OrbitModeLabel => IsRadioMode ? "ORBITAL RADIO ARRAY" : "ORBITAL APP ARRAY";
    public string SelectedOrbitName { get => _selectedOrbitName; private set => SetProperty(ref _selectedOrbitName, value); }
    public string SelectedOrbitSubtitle { get => _selectedOrbitSubtitle; private set => SetProperty(ref _selectedOrbitSubtitle, value); }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string OrbitSummaryTitle
    {
        get => _orbitSummaryTitle;
        private set => SetProperty(ref _orbitSummaryTitle, value);
    }

    public string OrbitSummaryText
    {
        get => _orbitSummaryText;
        private set => SetProperty(ref _orbitSummaryText, value);
    }

    public string OrbitPageLabel => OrbitPageCount == 0 ? "Page 0 / 0" : $"Page {_orbitPageIndex + 1} / {OrbitPageCount}";

    public string CurrentTimeText
    {
        get => _currentTimeText;
        private set => SetProperty(ref _currentTimeText, value);
    }

    public string CurrentDateText
    {
        get => _currentDateText;
        private set => SetProperty(ref _currentDateText, value);
    }

    public string CurrentFullDateText
    {
        get => _currentFullDateText;
        private set => SetProperty(ref _currentFullDateText, value);
    }

    public double CpuUsage { get => _cpuUsage; private set => SetProperty(ref _cpuUsage, value); }
    public double RamUsage { get => _ramUsage; private set => SetProperty(ref _ramUsage, value); }
    public double DiskUsage { get => _diskUsage; private set => SetProperty(ref _diskUsage, value); }
    public string RamSummary { get => _ramSummary; private set => SetProperty(ref _ramSummary, value); }
    public string DiskSummary { get => _diskSummary; private set => SetProperty(ref _diskSummary, value); }
    public string SystemUptimeText { get => _systemUptimeText; private set => SetProperty(ref _systemUptimeText, value); }

    public async Task InitializeAsync()
    {
        try
        {
            StatusText = "Loading Titanium Orbital library...";
            _settings = await _catalogService.LoadSettingsAsync();
            OnPropertyChanged(nameof(CustomBackgroundPath));
            ReplaceItems(Favorites, _settings.Favorites);
            RebuildOrbit();
            await RefreshManagedLibraryAsync();
            StatusText = $"Loaded {ManagedShortcutsCount} shortcuts, {ManagedFoldersCount} folders, {ManagedFilesCount} files";
            await Task.Yield();
            _ = RefreshIndexAsync();
            _ = SearchRadioStationsAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Startup error: {ex.Message}";
        }
    }

    public async Task SearchRadioStationsAsync()
    {
        try
        {
            RadioStatusText = $"Loading {RadioRegion.ToLowerInvariant()} radio directory...";
            List<RadioStation> stations;
            if (RadioRegion == "EUROPE")
            {
                string[] europeanCodes = ["GB", "IE", "FR", "DE", "ES", "PT", "IT", "NL", "BE", "CH", "AT", "SE", "NO", "DK", "FI", "PL", "CZ", "GR"];
                var tasks = europeanCodes.Select(code => FetchRadioStationsAsync(code, 35));
                stations = (await Task.WhenAll(tasks)).SelectMany(items => items).ToList();
            }
            else
            {
                stations = await FetchRadioStationsAsync(RadioRegion == "UK" ? "GB" : null, RadioRegion == "WORLD" ? 500 : 300);
            }

            ReplaceItems(RadioStations, stations.Where(station => !string.IsNullOrWhiteSpace(station.StreamUrl)));
            RadioStatusText = $"{RadioStations.Count} stations available";
            if (IsRadioMode)
            {
                _orbitPageIndex = 0;
                RebuildOrbit();
            }
        }
        catch (Exception ex)
        {
            RadioStatusText = $"Radio directory unavailable: {ex.Message}";
        }
    }

    public void SetRadioStatus(string message) => RadioStatusText = message;

    public async Task SetCustomBackgroundAsync(string? path)
    {
        _settings.CustomBackgroundPath = path;
        OnPropertyChanged(nameof(CustomBackgroundPath));
        await _catalogService.SaveSettingsAsync(_settings);
        StatusText = string.IsNullOrWhiteSpace(path) ? "Built-in circuit wallpaper restored" : "Custom wallpaper applied";
    }

    public async Task SetRadioRegionAsync(string region)
    {
        RadioRegion = region;
        await SearchRadioStationsAsync();
    }

    public void SetOrbitMode(bool radioMode)
    {
        IsRadioMode = radioMode;
        _orbitPageIndex = 0;
        _orbitRotationDegrees = 0;
        OnPropertyChanged(nameof(OrbitModeLabel));
        OnPropertyChanged(nameof(OrbitPageCount));
        RebuildOrbit();
    }

    public void RotateOrbit(double degrees)
    {
        _orbitRotationDegrees = (_orbitRotationDegrees + degrees) % 360;
        PositionOrbitNodes();
    }

    private async Task<List<RadioStation>> FetchRadioStationsAsync(string? countryCode, int limit)
    {
        var parts = new List<string>
        {
            "hidebroken=true", "order=votes", "reverse=true", $"limit={limit}"
        };
        if (!string.IsNullOrWhiteSpace(RadioSearchText)) parts.Add($"name={Uri.EscapeDataString(RadioSearchText.Trim())}");
        if (!string.IsNullOrWhiteSpace(countryCode)) parts.Add($"countrycode={countryCode}");
        return await RadioClient.GetFromJsonAsync<List<RadioStation>>($"json/stations/search?{string.Join('&', parts)}") ?? [];
    }

    public async Task RefreshIndexAsync()
    {
        _refreshCancellationSource?.Cancel();
        _refreshCancellationSource = new CancellationTokenSource();

        try
        {
            var progress = new Progress<string>(message => StatusText = message);
            var indexedItems = await Task.Run(
                async () => await _catalogService.BuildIndexAsync(progress, _refreshCancellationSource.Token),
                _refreshCancellationSource.Token);
            ReplaceItems(_catalog, indexedItems);
            UpdateSearchResults();
            StatusText = $"Indexed {_catalog.Count:N0} items";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Index refresh cancelled";
        }
        catch (Exception ex)
        {
            StatusText = $"Index error: {ex.Message}";
        }
    }

    public async Task AddFavoriteFromPathAsync(string path, LauncherItemKind kind)
    {
        var item = _catalogService.CreateItemFromPath(path, kind, "manual");
        await AddFavoriteAsync(item);
    }

    public async Task OrganizeDesktopAsync()
    {
        var organized = await _catalogService.OrganizeDesktopAsync();
        ReplaceItems(Favorites, organized);
        RebuildOrbit();
        await PersistFavoritesAsync();
        await RefreshManagedLibraryAsync();
        await RefreshIndexAsync();
        StatusText = $"Desktop organized: moved {Math.Max(0, organized.Count - 3)} items into Desktop Orbit Library";
    }

    public async Task RefreshManagedLibraryAsync()
    {
        var snapshot = await _catalogService.LoadManagedLibraryAsync();
        ReplaceItems(ManagedShortcuts, snapshot.Shortcuts);
        ReplaceItems(ManagedFolders, snapshot.Folders);
        ReplaceItems(ManagedFiles, snapshot.Files);
        ReplaceItems(RecentShortcuts, snapshot.Shortcuts.Take(2));
        if (_orbitPageIndex >= OrbitPageCount)
        {
            _orbitPageIndex = Math.Max(0, OrbitPageCount - 1);
        }
        OnPropertyChanged(nameof(ManagedShortcutsCount));
        OnPropertyChanged(nameof(ManagedFoldersCount));
        OnPropertyChanged(nameof(ManagedFilesCount));
        OnPropertyChanged(nameof(OrbitPageCount));
        OnPropertyChanged(nameof(OrbitPageLabel));
        RebuildOrbit();
    }

    public int ManagedShortcutsCount => ManagedShortcuts.Count;

    public int ManagedFoldersCount => ManagedFolders.Count;

    public int ManagedFilesCount => ManagedFiles.Count;

    public int OrbitPageCount
    {
        get
        {
            var count = IsRadioMode ? RadioStations.Count : GetOrbitShortcutSource().Count;
            return count == 0 ? 0 : (int)Math.Ceiling(count / (double)OrbitPageSize);
        }
    }

    public LauncherItem GetLibraryRootItem(string section) => section switch
    {
        "Shortcuts" => _catalogService.CreateItemFromPath(_catalogService.GetShortcutsRoot(), LauncherItemKind.Folder, "managed-root"),
        "Folders" => _catalogService.CreateItemFromPath(_catalogService.GetFoldersRoot(), LauncherItemKind.Folder, "managed-root"),
        _ => _catalogService.CreateItemFromPath(_catalogService.GetFilesRoot(), LauncherItemKind.Folder, "managed-root")
    };

    public void NextOrbitPage()
    {
        if (OrbitPageCount == 0)
        {
            return;
        }

        _orbitPageIndex = (_orbitPageIndex + 1) % OrbitPageCount;
        RebuildOrbit();
    }

    public void PreviousOrbitPage()
    {
        if (OrbitPageCount == 0)
        {
            return;
        }

        _orbitPageIndex = (_orbitPageIndex - 1 + OrbitPageCount) % OrbitPageCount;
        RebuildOrbit();
    }

    public async Task AddFavoriteAsync(LauncherItem item, bool saveAfterEach = true)
    {
        if (Favorites.Any(existing => existing.TargetPath.Equals(item.TargetPath, StringComparison.OrdinalIgnoreCase)))
        {
            StatusText = $"{item.DisplayName} is already in the orbit";
            return;
        }

        item.IsFavorite = true;
        Favorites.Add(item);
        RebuildOrbit();

        if (saveAfterEach)
        {
            await PersistFavoritesAsync();
        }

        StatusText = $"Pinned {item.DisplayName}";
    }

    public async Task RemoveFavoriteAsync(LauncherItem item)
    {
        var existing = Favorites.FirstOrDefault(candidate => candidate.TargetPath.Equals(item.TargetPath, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            return;
        }

        Favorites.Remove(existing);
        RebuildOrbit();
        await PersistFavoritesAsync();
        StatusText = $"Removed {item.DisplayName}";
    }

    public void OpenItem(LauncherItem item)
    {
        try
        {
            LauncherCatalogService.Open(item);
            StatusText = $"Opened {item.DisplayName}";
        }
        catch (Exception ex)
        {
            StatusText = $"Launch error: {ex.Message}";
        }
    }

    private async Task PersistFavoritesAsync()
    {
        _settings.Favorites = Favorites.ToList();
        await _catalogService.SaveSettingsAsync(_settings);
    }

    private void UpdateSearchResults()
    {
        IEnumerable<LauncherItem> results = _catalog;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            results = results.Where(item =>
                item.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                item.TargetPath.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        results = results
            .OrderByDescending(item => !string.IsNullOrWhiteSpace(SearchText) &&
                                       item.DisplayName.StartsWith(SearchText, StringComparison.OrdinalIgnoreCase))
            .ThenBy(item => item.DisplayName)
            .Take(80);

        ReplaceItems(SearchResults, results);
        OrbitSummaryTitle = SearchResults.Count == 0 ? "Orbit" : $"{SearchResults.Count}";
        OrbitSummaryText = string.IsNullOrWhiteSpace(SearchText)
            ? "Pinned items orbit around the center. Search across apps, folders, and indexed files from all fixed drives."
            : $"Results for \"{SearchText}\"";
    }

    private void RebuildOrbit()
    {
        OrbitItems.Clear();
        if (IsRadioMode)
        {
            foreach (var station in RadioStations.Skip(_orbitPageIndex * OrbitPageSize).Take(OrbitPageSize))
            {
                OrbitItems.Add(new OrbitNode
                {
                    RadioStation = station,
                    DisplayName = station.Name,
                    Subtitle = station.Country,
                    Glyph = station.Initials,
                    AccentBrush = OrbitBrushes[OrbitItems.Count % OrbitBrushes.Length]
                });
            }
        }
        else
        {
            foreach (var app in GetOrbitShortcutSource().Skip(_orbitPageIndex * OrbitPageSize).Take(OrbitPageSize))
            {
                OrbitItems.Add(new OrbitNode
                {
                    SourceItem = app,
                    DisplayName = app.DisplayName,
                    Subtitle = app.Subtitle,
                    IconPath = app.TargetPath,
                    AccentBrush = OrbitBrushes[OrbitItems.Count % OrbitBrushes.Length]
                });
            }
        }

        if (OrbitItems.Count == 0)
        {
            OrbitSummaryTitle = "Orbit";
            OrbitSummaryText = IsRadioMode ? "No radio stations are loaded yet." : "No app shortcuts are loaded into the ring yet.";
            SelectedOrbitName = "READY";
            SelectedOrbitSubtitle = IsRadioMode ? "Choose a radio region" : "Add an application";
            return;
        }
        PositionOrbitNodes();
        OrbitSummaryTitle = $"{OrbitItems.Count}";
        OrbitSummaryText = IsRadioMode
            ? $"Rotate the dial to select a station. {OrbitPageLabel}"
            : $"Rotate the dial to select an app. {OrbitPageLabel}";
        OnPropertyChanged(nameof(OrbitPageLabel));
    }

    private void PositionOrbitNodes()
    {
        if (OrbitItems.Count == 0) return;
        const double center = 290;
        const double radius = 230;
        const double itemWidth = 112;
        const double itemHeight = 108;
        var step = 360d / OrbitItems.Count;
        var activeIndex = 0;
        var closest = double.MaxValue;

        for (var index = 0; index < OrbitItems.Count; index++)
        {
            var angle = -90 + index * step + _orbitRotationDegrees;
            var radians = angle * Math.PI / 180d;
            var node = OrbitItems[index];
            node.X = center + radius * Math.Cos(radians) - itemWidth / 2;
            node.Y = center + radius * Math.Sin(radians) - itemHeight / 2;
            var normalized = ((angle + 90) % 360 + 360) % 360;
            var distance = Math.Min(normalized, 360 - normalized);
            if (distance < closest) { closest = distance; activeIndex = index; }
        }

        for (var index = 0; index < OrbitItems.Count; index++) OrbitItems[index].IsActive = index == activeIndex;
        var active = OrbitItems[activeIndex];
        SelectedOrbitName = active.DisplayName;
        SelectedOrbitSubtitle = active.Subtitle;
    }

    private List<LauncherItem> GetOrbitShortcutSource()
    {
        // Keep manually pinned apps first, then fill the array from the managed shortcut library.
        // Grouping by target path prevents the same app appearing twice after desktop organization.
        return Favorites
            .Concat(ManagedShortcuts)
            .Where(item => item.Kind == LauncherItemKind.App)
            .Where(item => !item.DisplayName.Equals("Desktop Orbit", StringComparison.OrdinalIgnoreCase))
            .Where(item => !item.DisplayName.Equals("Titanium Orbital Launcher", StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => item.TargetPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(item => item.IsFavorite)
            .ThenBy(item => item.DisplayName)
            .ToList();
    }

    private static void ReplaceItems<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void UpdateClock()
    {
        CurrentTimeText = DateTime.Now.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        CurrentDateText = DateTime.Now.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
        CurrentFullDateText = string.Empty;
    }

    private void UpdateTelemetry()
    {
        var snapshot = _telemetryService.Read();
        CpuUsage = snapshot.CpuUsage;
        RamUsage = snapshot.RamUsage;
        DiskUsage = snapshot.DiskUsage;
        RamSummary = snapshot.RamSummary;
        DiskSummary = snapshot.DiskSummary;
        SystemUptimeText = snapshot.UptimeText;
    }
}
