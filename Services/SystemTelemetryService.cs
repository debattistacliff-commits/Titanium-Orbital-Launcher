using System.IO;
using System.Runtime.InteropServices;
using DesktopOrbit.Models;

namespace DesktopOrbit.Services;

public sealed class SystemTelemetryService
{
    private ulong _previousIdle;
    private ulong _previousKernel;
    private ulong _previousUser;
    private bool _hasCpuSample;

    public SystemTelemetrySnapshot Read()
    {
        var cpuUsage = ReadCpuUsage();
        var (ramUsage, ramSummary) = ReadMemoryUsage();
        var (diskUsage, diskSummary) = ReadSystemDriveUsage();
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        var uptimeText = uptime.TotalDays >= 1
            ? $"UP {(int)uptime.TotalDays}D {uptime.Hours:00}H"
            : $"UP {uptime.Hours:00}:{uptime.Minutes:00}";

        return new SystemTelemetrySnapshot(cpuUsage, ramUsage, diskUsage, ramSummary, diskSummary, uptimeText);
    }

    private double ReadCpuUsage()
    {
        if (!GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
        {
            return 0;
        }

        var idle = ToUInt64(idleTime);
        var kernel = ToUInt64(kernelTime);
        var user = ToUInt64(userTime);

        if (!_hasCpuSample)
        {
            _previousIdle = idle;
            _previousKernel = kernel;
            _previousUser = user;
            _hasCpuSample = true;
            return 0;
        }

        var idleDelta = idle - _previousIdle;
        var kernelDelta = kernel - _previousKernel;
        var userDelta = user - _previousUser;
        var totalDelta = kernelDelta + userDelta;

        _previousIdle = idle;
        _previousKernel = kernel;
        _previousUser = user;

        if (totalDelta == 0)
        {
            return 0;
        }

        return Math.Clamp((totalDelta - idleDelta) * 100d / totalDelta, 0, 100);
    }

    private static (double Usage, string Summary) ReadMemoryUsage()
    {
        var status = new MemoryStatusEx();
        if (!GlobalMemoryStatusEx(status) || status.TotalPhysical == 0)
        {
            return (0, "Memory data unavailable");
        }

        var used = status.TotalPhysical - status.AvailablePhysical;
        var usage = used * 100d / status.TotalPhysical;
        return (usage, $"{ToGiB(used):F1} / {ToGiB(status.TotalPhysical):F1} GB used");
    }

    private static (double Usage, string Summary) ReadSystemDriveUsage()
    {
        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory);
            var drive = DriveInfo.GetDrives().FirstOrDefault(candidate =>
                candidate.IsReady && string.Equals(candidate.RootDirectory.FullName, root, StringComparison.OrdinalIgnoreCase));
            if (drive is null || drive.TotalSize == 0)
            {
                return (0, "System drive unavailable");
            }

            var used = drive.TotalSize - drive.AvailableFreeSpace;
            var usage = used * 100d / drive.TotalSize;
            return (usage, $"{ToGiB(used):F0} / {ToGiB(drive.TotalSize):F0} GB used on {drive.Name.TrimEnd('\\')}");
        }
        catch
        {
            return (0, "System drive unavailable");
        }
    }

    private static double ToGiB(ulong bytes) => bytes / 1024d / 1024d / 1024d;
    private static double ToGiB(long bytes) => bytes / 1024d / 1024d / 1024d;

    private static ulong ToUInt64(FileTime value) => ((ulong)value.High << 32) | value.Low;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MemoryStatusEx
    {
        public uint Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }
}
