using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DesktopOrbit.Models;

public sealed class OrbitNode : INotifyPropertyChanged
{
    private double _x;
    private double _y;
    private bool _isActive;

    public LauncherItem? SourceItem { get; init; }
    public RadioStation? RadioStation { get; init; }
    public required string DisplayName { get; init; }
    public string Subtitle { get; init; } = string.Empty;
    public string IconPath { get; init; } = string.Empty;
    public string Glyph { get; init; } = string.Empty;
    public required System.Windows.Media.Brush AccentBrush { get; init; }

    public double X { get => _x; set => SetProperty(ref _x, value); }
    public double Y { get => _y; set => SetProperty(ref _y, value); }
    public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
