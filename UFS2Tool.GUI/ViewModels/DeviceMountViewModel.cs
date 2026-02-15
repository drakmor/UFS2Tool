// Copyright (c) 2026, SvenGDK
// Licensed under the BSD 2-Clause License. See LICENSE file for details.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
namespace UFS2Tool.GUI.ViewModels;

public partial class DeviceMountViewModel : ViewModelBase
{
    private readonly ObservableCollection<string> _outputLog;

    [ObservableProperty]
    private string _imagePath = "";

    [ObservableProperty]
    private string _mountDriveLetter = "Z";

    [ObservableProperty]
    private bool _mountReadWrite;

    [ObservableProperty]
    private string _devicePath = "";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _deviceInfoText = "";

    public DeviceMountViewModel(ObservableCollection<string> outputLog)
    {
        _outputLog = outputLog;
    }

    [RelayCommand]
    private async Task ShowDeviceInfoAsync()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _outputLog.Add("[DevInfo] Device info is only available on Windows.");
            return;
        }
        if (string.IsNullOrWhiteSpace(DevicePath))
        {
            _outputLog.Add("[Error] Please specify a device path (e.g., \\\\.\\PhysicalDrive1).");
            return;
        }
        IsRunning = true;
        _outputLog.Add($"[DevInfo] Querying device information for '{DevicePath}'...");
        try
        {
            await Task.Run(() =>
            {
#pragma warning disable CA1416 // Platform compatibility - guarded by RuntimeInformation check above
                long size = UFS2Tool.DriveIO.GetDeviceSize(DevicePath);
                int sectorSize = UFS2Tool.DriveIO.GetSectorSize(DevicePath);
#pragma warning restore CA1416
                long sectorCount = size / sectorSize;
                double sizeMB = size / (1024.0 * 1024.0);
                double sizeGB = sizeMB / 1024.0;
                string humanSize = sizeGB >= 1.0 ? $"{sizeGB:F2} GB" : $"{sizeMB:F2} MB";
                string text = $"Device: {DevicePath}\nSize: {size:N0} bytes ({humanSize})\nSectors: {sectorCount:N0}\nSector Size: {sectorSize}";
                DeviceInfoText = text;
                _outputLog.Add($"[DevInfo] {text}");
            });
        }
        catch (Exception ex)
        {
            _outputLog.Add($"[Error] {ex.Message}{(ex.InnerException != null ? $" — {ex.InnerException.Message}" : "")}");
        }
        finally { IsRunning = false; }
    }

    [RelayCommand]
    private Task MountAsync()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _outputLog.Add("[Mount] Mounting is only available on Windows (requires Dokan driver).");
            return Task.CompletedTask;
        }
        if (string.IsNullOrWhiteSpace(ImagePath))
        {
            _outputLog.Add("[Error] Please specify an image file path.");
            return Task.CompletedTask;
        }
        if (string.IsNullOrWhiteSpace(MountDriveLetter))
        {
            _outputLog.Add("[Error] Please specify a drive letter.");
            return Task.CompletedTask;
        }

        _outputLog.Add($"[Mount] Mounting '{ImagePath}' as {MountDriveLetter}:\\ (Read{(MountReadWrite ? "/Write" : " Only")})...");
        _outputLog.Add("[Mount] Note: Mounting requires the Dokan driver to be installed.");
        _outputLog.Add("[Mount] Use the CLI tool for mount operations: UFS2Tool mount_udf <image> <drive-letter>");

        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task UnmountAsync()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _outputLog.Add("[Unmount] Unmounting is only available on Windows.");
            return Task.CompletedTask;
        }
        if (string.IsNullOrWhiteSpace(MountDriveLetter))
        {
            _outputLog.Add("[Error] Please specify a drive letter to unmount.");
            return Task.CompletedTask;
        }

        _outputLog.Add($"[Unmount] Use the CLI tool for unmount operations: UFS2Tool umount_udf {MountDriveLetter}");
        return Task.CompletedTask;
    }
}
