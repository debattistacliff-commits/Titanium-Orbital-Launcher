using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Media;
using System.IO;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using MediaColor = System.Windows.Media.Color;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;
using DesktopOrbit.Models;
using DesktopOrbit.ViewModels;
using Forms = System.Windows.Forms;

namespace DesktopOrbit;

public partial class MainWindow : Window
{
    private static readonly HashSet<string> VideoWallpaperExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".wmv", ".avi"
    };

    private const int HotkeyId = 0x1200;
    private const uint ModControl = 0x0002;
    private const uint ModAlt = 0x0001;
    private const uint VkSpace = 0x20;
    private bool _allowShutdown;
    private Forms.NotifyIcon? _notifyIcon;
    private HwndSource? _hwndSource;
    private bool _isDialDragging;
    private double _lastDialAngle;
    private double _detentTravel;
    private readonly SoundPlayer _detentPlayer = CreateDetentPlayer();
    private readonly DispatcherTimer _visualizerTimer;
    private readonly Random _visualizerRandom = new();

    public MainWindow()
    {
        InitializeComponent();
        _visualizerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(90) };
        _visualizerTimer.Tick += (_, _) => AnimateRadioMonitor();
        DataContext = new MainViewModel();
        Loaded += OnLoaded;
        Closing += OnClosing;
        StateChanged += OnStateChanged;
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        SetupTrayIcon();
        RegisterHotkey();
        await ViewModel.InitializeAsync();
        ApplyWallpaper(ViewModel.CustomBackgroundPath);
    }

    private void SetupTrayIcon()
    {
        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "Titanium Orbital Launcher",
            Visible = true,
            Icon = System.Drawing.SystemIcons.Application,
            ContextMenuStrip = new Forms.ContextMenuStrip()
        };
        _notifyIcon.DoubleClick += (_, _) => ToggleVisibility();
        _notifyIcon.ContextMenuStrip!.Items.Add("Show / Hide", null, (_, _) => ToggleVisibility());
        _notifyIcon.ContextMenuStrip.Items.Add("Refresh Index", null, async (_, _) => await ViewModel.RefreshIndexAsync());
        _notifyIcon.ContextMenuStrip.Items.Add("Exit", null, (_, _) => ExitApplication());
    }

    private void RegisterHotkey()
    {
        var helper = new WindowInteropHelper(this);
        _hwndSource = HwndSource.FromHwnd(helper.EnsureHandle());
        _hwndSource?.AddHook(WndProc);
        RegisterHotKey(helper.Handle, HotkeyId, ModControl | ModAlt, VkSpace);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WmHotKey = 0x0312;

        if (msg == WmHotKey && wParam.ToInt32() == HotkeyId)
        {
            ToggleVisibility();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void ToggleVisibility()
    {
        if (IsVisible && WindowState != WindowState.Minimized)
        {
            Hide();
            return;
        }

        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
    }

    private void HideOrbit_Click(object sender, RoutedEventArgs e) => Hide();

    private async void RefreshIndex_Click(object sender, RoutedEventArgs e) => await ViewModel.RefreshIndexAsync();

    private async void AddApp_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose an application or shortcut",
            Filter = "Applications and Shortcuts|*.exe;*.lnk;*.url|All files|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            await ViewModel.AddFavoriteFromPathAsync(dialog.FileName, LauncherItemKind.App);
        }
    }

    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Choose a folder to add to the orbit"
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            await ViewModel.AddFavoriteFromPathAsync(dialog.SelectedPath, LauncherItemKind.Folder);
        }
    }

    private async void ChooseBackground_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose a launcher image or video wallpaper",
            Filter = "Wallpaper files|*.mp4;*.wmv;*.avi;*.png;*.jpg;*.jpeg;*.webp;*.bmp|Video files|*.mp4;*.wmv;*.avi|Image files|*.png;*.jpg;*.jpeg;*.webp;*.bmp|All files|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true) return;

        try
        {
            var backgroundsRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DesktopOrbit", "Backgrounds");
            Directory.CreateDirectory(backgroundsRoot);
            var extension = Path.GetExtension(dialog.FileName);
            var storedPath = Path.Combine(backgroundsRoot, $"wallpaper-{DateTime.Now:yyyyMMdd-HHmmss}{extension}");
            File.Copy(dialog.FileName, storedPath, false);
            ApplyWallpaper(storedPath);
            await ViewModel.SetCustomBackgroundAsync(storedPath);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"Could not apply that background.\n\n{ex.Message}", "Titanium Orbital", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ResetBackground_Click(object sender, RoutedEventArgs e)
    {
        ApplyWallpaper(null);
        await ViewModel.SetCustomBackgroundAsync(null);
    }

    private void ApplyWallpaper(string? path)
    {
        StopVideoWallpaper();

        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            if (VideoWallpaperExtensions.Contains(Path.GetExtension(path)))
            {
                CircuitWallpaper.Visibility = Visibility.Collapsed;
                WallpaperShade.Fill = new MediaSolidColorBrush(MediaColor.FromArgb(0x76, 0x06, 0x09, 0x0B));
                VideoWallpaper.Source = new Uri(path, UriKind.Absolute);
                VideoWallpaper.Visibility = Visibility.Visible;
                VideoWallpaper.Position = TimeSpan.Zero;
                VideoWallpaper.Play();
                return;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            WallpaperShade.Fill = new MediaSolidColorBrush(MediaColor.FromArgb(0xB5, 0x06, 0x09, 0x0B));
            CircuitWallpaper.Visibility = Visibility.Visible;
            CircuitWallpaper.Source = bitmap;
            return;
        }

        WallpaperShade.Fill = new MediaSolidColorBrush(MediaColor.FromArgb(0xB5, 0x06, 0x09, 0x0B));
        CircuitWallpaper.Visibility = Visibility.Visible;
        CircuitWallpaper.Source = new BitmapImage(new Uri(
            "pack://application:,,,/Assets/circuit-wallpaper-v1.png",
            UriKind.Absolute));
    }

    private void StopVideoWallpaper()
    {
        VideoWallpaper.Stop();
        VideoWallpaper.Source = null;
        VideoWallpaper.Visibility = Visibility.Collapsed;
    }

    private void VideoWallpaper_MediaEnded(object sender, RoutedEventArgs e)
    {
        VideoWallpaper.Position = TimeSpan.Zero;
        VideoWallpaper.Play();
    }

    private void VideoWallpaper_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        StopVideoWallpaper();
        WallpaperShade.Fill = new MediaSolidColorBrush(MediaColor.FromArgb(0xB5, 0x06, 0x09, 0x0B));
        CircuitWallpaper.Visibility = Visibility.Visible;
        CircuitWallpaper.Source = new BitmapImage(new Uri(
            "pack://application:,,,/Assets/circuit-wallpaper-v1.png",
            UriKind.Absolute));
        ViewModel.SetRadioStatus($"Video wallpaper could not play: {e.ErrorException?.Message ?? "unsupported media"}");
    }

    private async void OrganizeDesktop_Click(object sender, RoutedEventArgs e) => await ViewModel.OrganizeDesktopAsync();

    private void OrbitItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: OrbitNode node }) return;
        if (node.SourceItem is not null)
        {
            ViewModel.OpenItem(node.SourceItem);
        }
        else if (node.RadioStation is not null)
        {
            PlayRadioStation(node.RadioStation);
        }
    }

    private void SearchResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SearchResultsList.SelectedItem is LauncherItem item)
        {
            ViewModel.OpenItem(item);
        }
    }

    private void Favorites_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.ListBox { SelectedItem: LauncherItem item })
        {
            ViewModel.OpenItem(item);
        }
    }

    private void ManagedItems_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.ListBox { SelectedItem: LauncherItem item })
        {
            ViewModel.OpenItem(item);
        }
    }

    private async void AddSelectedSearchResultToOrbit_Click(object sender, RoutedEventArgs e)
    {
        if (SearchResultsList.SelectedItem is LauncherItem item)
        {
            await ViewModel.AddFavoriteAsync(item);
        }
    }

    private async void RemoveFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: LauncherItem item })
        {
            await ViewModel.RemoveFavoriteAsync(item);
        }
    }

    private void OpenManagedRoot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string section })
        {
            ViewModel.OpenItem(ViewModel.GetLibraryRootItem(section));
        }
    }

    private void PreviousOrbitPage_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.PreviousOrbitPage();
        PlayDetent();
    }

    private void NextOrbitPage_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.NextOrbitPage();
        PlayDetent();
    }

    private void AppsMode_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SetOrbitMode(false);
        AppsModeButton.Style = (Style)FindResource("Cyan3DButtonStyle");
        RadioModeButton.Style = (Style)FindResource(typeof(System.Windows.Controls.Button));
        PlayDetent();
    }

    private void RadioMode_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SetOrbitMode(true);
        RadioModeButton.Style = (Style)FindResource("Cyan3DButtonStyle");
        AppsModeButton.Style = (Style)FindResource(typeof(System.Windows.Controls.Button));
        PlayDetent();
    }

    private async void RadioRegion_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string region })
        {
            await ViewModel.SetRadioRegionAsync(region);
            ViewModel.SetOrbitMode(true);
            RadioModeButton.Style = (Style)FindResource("Cyan3DButtonStyle");
            AppsModeButton.Style = (Style)FindResource(typeof(System.Windows.Controls.Button));
            PlayDetent();
        }
    }

    private async void SearchRadio_Click(object sender, RoutedEventArgs e) => await ViewModel.SearchRadioStationsAsync();

    private void RadioStations_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (RadioStationsList.SelectedItem is RadioStation station) PlayRadioStation(station);
    }

    private void PlayRadioStation(RadioStation station)
    {
        if (!Uri.TryCreate(station.StreamUrl, UriKind.Absolute, out var streamUri)) return;
        try
        {
            RadioPlayer.Stop();
            RadioPlayer.Source = streamUri;
            RadioPlayer.Play();
            RadioMonitorStation.Text = station.Name;
            RadioMonitor.Visibility = Visibility.Visible;
            _visualizerTimer.Start();
            ViewModel.SetRadioStatus($"NOW PLAYING  •  {station.Name}  •  {station.Country}");
        }
        catch (Exception ex)
        {
            ViewModel.SetRadioStatus($"Unable to tune station: {ex.Message}");
        }
    }

    private void DialSurface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDialDragging = true;
        _lastDialAngle = GetDialAngle(e.GetPosition(DialSurface));
        _detentTravel = 0;
        DialSurface.CaptureMouse();
    }

    private void DialSurface_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDialDragging || e.LeftButton != MouseButtonState.Pressed) return;
        var angle = GetDialAngle(e.GetPosition(DialSurface));
        var delta = angle - _lastDialAngle;
        if (delta > 180) delta -= 360;
        if (delta < -180) delta += 360;
        _lastDialAngle = angle;
        _detentTravel += delta;
        ViewModel.RotateOrbit(delta);
        if (Math.Abs(_detentTravel) >= 36)
        {
            PlayDetent();
            _detentTravel %= 36;
        }
    }

    private void DialSurface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => EndDialDrag();
    private void DialSurface_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) EndDialDrag();
    }

    private void EndDialDrag()
    {
        _isDialDragging = false;
        DialSurface.ReleaseMouseCapture();
    }

    private static double GetDialAngle(System.Windows.Point point) => Math.Atan2(point.Y - 290, point.X - 290) * 180 / Math.PI;

    private void PlayDetent()
    {
        try { _detentPlayer.Play(); } catch { SystemSounds.Beep.Play(); }
    }

    private static SoundPlayer CreateDetentPlayer()
    {
        const int sampleRate = 22050;
        const int sampleCount = 900;
        var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + sampleCount * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1);
            writer.Write(sampleRate); writer.Write(sampleRate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(sampleCount * 2);
            var random = new Random(42);
            for (var i = 0; i < sampleCount; i++)
            {
                var envelope = Math.Exp(-i / 115d);
                var tone = Math.Sin(2 * Math.PI * 1150 * i / sampleRate) * 0.58;
                var noise = (random.NextDouble() * 2 - 1) * 0.42;
                writer.Write((short)((tone + noise) * envelope * short.MaxValue * 0.22));
            }
        }
        stream.Position = 0;
        return new SoundPlayer(stream);
    }

    private void StopRadio_Click(object sender, RoutedEventArgs e)
    {
        RadioPlayer.Stop();
        ViewModel.SetRadioStatus("Radio stopped");
        _visualizerTimer.Stop();
        RadioMonitor.Visibility = Visibility.Collapsed;
    }

    private void AnimateRadioMonitor()
    {
        var bars = new[] { Eq1, Eq2, Eq3, Eq4, Eq5, Eq6 };
        for (var index = 0; index < bars.Length; index++)
        {
            var phase = DateTime.Now.Millisecond / 1000d * Math.PI * 2 + index * 0.8;
            var wave = 8 + 17 * Math.Abs(Math.Sin(phase));
            bars[index].Height = Math.Clamp(wave + _visualizerRandom.NextDouble() * 7, 6, 30);
        }
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_allowShutdown)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            UnregisterHotKey(handle, HotkeyId);
        }

        _notifyIcon?.Dispose();
    }

    private void ExitApplication()
    {
        _allowShutdown = true;
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
