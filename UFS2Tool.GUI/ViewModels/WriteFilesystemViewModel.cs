// Copyright (c) 2026, SvenGDK
// Licensed under the BSD 2-Clause License. See LICENSE file for details.

using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
namespace UFS2Tool.GUI.ViewModels;

public partial class WriteFilesystemViewModel : ViewModelBase
{
    private readonly ObservableCollection<string> _outputLog;

    [ObservableProperty]
    private string _sourceImagePath = "";

    [ObservableProperty]
    private string _targetDevicePath = "";

    [ObservableProperty]
    private bool _verifyAfterWrite;

    [ObservableProperty]
    private bool _isWriting;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _progressText = "";

    [ObservableProperty]
    private bool _hasProgress;

    [ObservableProperty]
    private string _selectedDrive = "";

    public ObservableCollection<string> AvailableDrives { get; } = new();

    public WriteFilesystemViewModel(ObservableCollection<string> outputLog)
    {
        _outputLog = outputLog;
    }

    partial void OnSelectedDriveChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            // Extract device path from the display string (format: "/dev/sdX (size)" or "\\.\PhysicalDriveN (size)")
            int parenIndex = value.IndexOf(" (", StringComparison.Ordinal);
            string path = parenIndex > 0 ? value[..parenIndex] : value;

            // On Windows, prepend \\.\  so the path is usable with DriveIO
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !path.StartsWith(@"\\.\", StringComparison.Ordinal))
            {
                path = @"\\.\" + path;
            }

            TargetDevicePath = path;
        }
    }

    [RelayCommand]
    private void RefreshDrives()
    {
        AvailableDrives.Clear();
        _outputLog.Add("[Write] Refreshing available drives...");

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                RefreshDrivesWindows();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                RefreshDrivesLinux();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                RefreshDrivesMacOS();
            }
            else
            {
                _outputLog.Add("[Write] Unsupported platform for drive enumeration.");
            }

            if (AvailableDrives.Count == 0)
                _outputLog.Add("[Write] No removable or physical drives found.");
            else
                _outputLog.Add($"[Write] Found {AvailableDrives.Count} drive(s).");
        }
        catch (Exception ex)
        {
            _outputLog.Add($"[Error] Failed to enumerate drives: {ex.Message}");
        }
    }

    private void RefreshDrivesWindows()
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType is DriveType.Removable or DriveType.Fixed)
            {
                try
                {
                    string label = drive.IsReady ? drive.VolumeLabel : "";
                    string size = drive.IsReady ? FormatSize(drive.TotalSize) : "N/A";
                    string display = string.IsNullOrEmpty(label)
                        ? $"{drive.Name.TrimEnd('\\')} ({size})"
                        : $"{drive.Name.TrimEnd('\\')} {label} ({size})";
                    AvailableDrives.Add(display);
                }
                catch
                {
                    AvailableDrives.Add(drive.Name.TrimEnd('\\'));
                }
            }
        }
    }

    private void RefreshDrivesLinux()
    {
        try
        {
            string lsblkOutput = RunProcess("lsblk", "-dno NAME,SIZE,TYPE,RM");
            foreach (string line in lsblkOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = line.Trim();
                string[] parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3 && parts[2] == "disk")
                {
                    string devicePath = $"/dev/{parts[0]}";
                    string size = parts.Length >= 2 ? parts[1] : "N/A";
                    AvailableDrives.Add($"{devicePath} ({size})");
                }
            }
        }
        catch (Exception ex)
        {
            _outputLog.Add($"[Write] Could not enumerate Linux drives: {ex.Message}");
        }
    }

    private void RefreshDrivesMacOS()
    {
        try
        {
            // Try to list external physical disks first, fall back to all disks
            string listOutput = RunProcess("diskutil", "list external physical");
            if (string.IsNullOrWhiteSpace(listOutput))
                listOutput = RunProcess("diskutil", "list");

            foreach (string line in listOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("/dev/disk", StringComparison.Ordinal))
                {
                    // Extract the device path and description
                    int parenStart = trimmed.IndexOf('(');
                    if (parenStart > 0)
                    {
                        string devicePath = trimmed[..parenStart].Trim();
                        string description = trimmed[parenStart..].Trim();
                        AvailableDrives.Add($"{devicePath} {description}");
                    }
                    else
                    {
                        AvailableDrives.Add(trimmed);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _outputLog.Add($"[Write] Could not enumerate macOS drives: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task WriteFilesystemAsync()
    {
        if (string.IsNullOrWhiteSpace(SourceImagePath))
        {
            _outputLog.Add($"[Error] {Loc["WriteErrorNoImage"]}");
            return;
        }
        if (string.IsNullOrWhiteSpace(TargetDevicePath))
        {
            _outputLog.Add($"[Error] {Loc["WriteErrorNoTarget"]}");
            return;
        }
        if (!File.Exists(SourceImagePath))
        {
            _outputLog.Add($"[Error] {Loc["WriteErrorImageNotFound"]}");
            return;
        }

        IsWriting = true;
        HasProgress = true;
        ProgressPercent = 0;
        ProgressText = "";

        _outputLog.Add($"[Write] Writing '{SourceImagePath}' to '{TargetDevicePath}'...");

        try
        {
            await Task.Run(() =>
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    WriteOnWindows();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    WriteOnLinux();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    WriteOnMacOS();
                }
                else
                {
                    throw new PlatformNotSupportedException("Write operation is not supported on this platform.");
                }
            });

            _outputLog.Add($"[Write] {Loc["WriteSuccess"]}");
            ProgressPercent = 100;

            if (VerifyAfterWrite)
            {
                _outputLog.Add("[Write] Verifying written data...");
                ProgressPercent = 0;
                await Task.Run(() => VerifyWrite());
                _outputLog.Add($"[Write] {Loc["WriteVerifySuccess"]}");
                ProgressPercent = 100;
            }
        }
        catch (Exception ex)
        {
            _outputLog.Add($"[Error] {ex.Message}");
            if (ex.InnerException != null)
                _outputLog.Add($"  Detail: {ex.InnerException.Message}");
        }
        finally
        {
            IsWriting = false;
        }
    }

    private void WriteOnWindows()
    {
#pragma warning disable CA1416 // Platform compatibility - guarded by RuntimeInformation check
        using var sourceStream = new FileStream(SourceImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        long totalBytes = sourceStream.Length;

        using var targetStream = DriveIO.OpenDeviceStream(TargetDevicePath, readOnly: false, lockVolume: true);

        CopyWithProgress(sourceStream, targetStream, totalBytes);

        targetStream.Flush();
#pragma warning restore CA1416
    }

    private void WriteOnLinux()
    {
        using var sourceStream = new FileStream(SourceImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        long totalBytes = sourceStream.Length;

        using var targetStream = new FileStream(TargetDevicePath, FileMode.Open, FileAccess.Write, FileShare.None);

        CopyWithProgress(sourceStream, targetStream, totalBytes);

        targetStream.Flush();

        // Sync to ensure all data is written to disk
        try { RunProcess("sync", ""); } catch { /* best effort */ }
    }

    private void WriteOnMacOS()
    {
        // Unmount the disk first on macOS
        try
        {
            RunProcess("diskutil", $"unmountDisk {TargetDevicePath}");
            Dispatcher.UIThread.Post(() => _outputLog.Add($"[Write] Unmounted {TargetDevicePath}"));
        }
        catch
        {
            Dispatcher.UIThread.Post(() => _outputLog.Add($"[Write] Warning: Could not unmount {TargetDevicePath}. Continuing anyway..."));
        }

        // Use raw device for better write performance on macOS
        string rawDevice = TargetDevicePath.Replace("/dev/disk", "/dev/rdisk");

        using var sourceStream = new FileStream(SourceImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        long totalBytes = sourceStream.Length;

        using var targetStream = new FileStream(rawDevice, FileMode.Open, FileAccess.Write, FileShare.None);

        CopyWithProgress(sourceStream, targetStream, totalBytes);

        targetStream.Flush();

        // Sync to ensure all data is written to disk
        try { RunProcess("sync", ""); } catch { /* best effort */ }
    }

    private void CopyWithProgress(Stream source, Stream target, long totalBytes)
    {
        const int bufferSize = 1024 * 1024; // 1 MB buffer
        byte[] buffer = new byte[bufferSize];
        long bytesWritten = 0;
        int bytesRead;

        while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            target.Write(buffer, 0, bytesRead);
            bytesWritten += bytesRead;

            double percent = totalBytes > 0 ? (double)bytesWritten / totalBytes * 100.0 : 0;
            string text = $"{FormatSize(bytesWritten)} / {FormatSize(totalBytes)} ({percent:F1}%)";
            Dispatcher.UIThread.Post(() =>
            {
                ProgressPercent = percent;
                ProgressText = text;
            });
        }
    }

    private void VerifyWrite()
    {
        using var sourceStream = new FileStream(SourceImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        long totalBytes = sourceStream.Length;

        using Stream targetStream = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
#pragma warning disable CA1416
            ? DriveIO.OpenDeviceStream(TargetDevicePath, readOnly: true, lockVolume: false)
#pragma warning restore CA1416
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? new FileStream(TargetDevicePath.Replace("/dev/disk", "/dev/rdisk"), FileMode.Open, FileAccess.Read, FileShare.Read)
                : new FileStream(TargetDevicePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        const int bufferSize = 1024 * 1024;
        byte[] sourceBuffer = new byte[bufferSize];
        byte[] targetBuffer = new byte[bufferSize];
        long bytesVerified = 0;

        while (bytesVerified < totalBytes)
        {
            int sourceRead = sourceStream.Read(sourceBuffer, 0, sourceBuffer.Length);
            int targetRead = targetStream.Read(targetBuffer, 0, targetBuffer.Length);

            if (sourceRead != targetRead)
                throw new IOException($"Verification failed: read size mismatch at offset {bytesVerified}.");

            if (!sourceBuffer.AsSpan(0, sourceRead).SequenceEqual(targetBuffer.AsSpan(0, targetRead)))
                throw new IOException($"Verification failed: data mismatch at offset {bytesVerified}.");

            bytesVerified += sourceRead;
            double percent = totalBytes > 0 ? (double)bytesVerified / totalBytes * 100.0 : 0;
            string text = $"{Loc["WriteVerifying"]} {FormatSize(bytesVerified)} / {FormatSize(totalBytes)} ({percent:F1}%)";
            Dispatcher.UIThread.Post(() =>
            {
                ProgressPercent = percent;
                ProgressText = text;
            });
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        if (bytes >= 1024L * 1024)
            return $"{bytes / (1024.0 * 1024):F2} MB";
        if (bytes >= 1024L)
            return $"{bytes / 1024.0:F2} KB";
        return $"{bytes} B";
    }

    private static string RunProcess(string fileName, string arguments)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        process.Start();
        // Read stdout/stderr asynchronously to avoid deadlock when buffers fill up
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            try { process.Kill(); } catch { /* best effort */ }
            throw new TimeoutException($"Process '{fileName}' did not complete within 30 seconds.");
        }
        string output = outputTask.GetAwaiter().GetResult();
        _ = errorTask.GetAwaiter().GetResult();
        return output;
    }
}
