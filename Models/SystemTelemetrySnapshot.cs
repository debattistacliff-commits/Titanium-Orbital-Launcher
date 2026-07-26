namespace DesktopOrbit.Models;

public sealed record SystemTelemetrySnapshot(
    double CpuUsage,
    double RamUsage,
    double DiskUsage,
    string RamSummary,
    string DiskSummary,
    string UptimeText);
