// Copyright (c) 2026, SvenGDK
// Licensed under the BSD 2-Clause License. See LICENSE file for details.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using UFS2Tool;
namespace UFS2Tool.GUI.ViewModels;

public partial class MaintenanceViewModel : ViewModelBase
{
    private readonly ObservableCollection<string> _outputLog;

    [ObservableProperty]
    private string _imagePath = "";

    [ObservableProperty]
    private bool _isRunning;

    // TuneFS properties
    [ObservableProperty]
    private string _tuneVolumeName = "";

    [ObservableProperty]
    private bool _tuneSoftUpdates;

    [ObservableProperty]
    private bool _tuneSoftUpdatesJournal;

    [ObservableProperty]
    private string _tuneMinFreePercent = "";

    [ObservableProperty]
    private string _tuneOptimization = "";

    [ObservableProperty]
    private bool _tunePrintOnly;

    // GrowFS properties
    [ObservableProperty]
    private long _newSizeInMB = 200;

    [ObservableProperty]
    private bool _growDryRun;

    // FsckUFS properties
    [ObservableProperty]
    private bool _fsckPreen;

    [ObservableProperty]
    private bool _fsckDebug;

    [ObservableProperty]
    private string _fsckResultText = "";

    public MaintenanceViewModel(ObservableCollection<string> outputLog)
    {
        _outputLog = outputLog;
    }

    [RelayCommand]
    private async Task TuneFilesystemAsync()
    {
        if (!ValidateImagePath()) return;
        IsRunning = true;
        try
        {
            await Task.Run(() =>
            {
                using var image = new Ufs2Image(ImagePath);
                var sb = image.Superblock;
                bool modified = false;

                if (!string.IsNullOrWhiteSpace(TuneVolumeName))
                {
                    sb.VolumeName = TuneVolumeName;
                    modified = true;
                    _outputLog.Add($"[TuneFS] Volume name set to: {TuneVolumeName}");
                }

                if (TuneSoftUpdates)
                {
                    sb.Flags |= Ufs2Constants.FsDosoftdep;
                    modified = true;
                    _outputLog.Add("[TuneFS] Soft updates enabled.");
                }

                if (TuneSoftUpdatesJournal)
                {
                    sb.Flags |= Ufs2Constants.FsSuj;
                    modified = true;
                    _outputLog.Add("[TuneFS] Soft updates journaling enabled.");
                }

                if (!string.IsNullOrWhiteSpace(TuneMinFreePercent) && int.TryParse(TuneMinFreePercent, out int minfree))
                {
                    sb.MinFreePercent = minfree;
                    modified = true;
                    _outputLog.Add($"[TuneFS] Minimum free space set to: {minfree}%");
                }

                if (!string.IsNullOrWhiteSpace(TuneOptimization))
                {
                    sb.Optimization = TuneOptimization == "space" ? 1 : 0;
                    modified = true;
                    _outputLog.Add($"[TuneFS] Optimization preference set to: {TuneOptimization}");
                }

                if (modified && !TunePrintOnly)
                {
                    image.WriteSuperblockToAll();
                    _outputLog.Add("[TuneFS] Superblock updated successfully.");
                }
                else if (TunePrintOnly)
                {
                    _outputLog.Add("[TuneFS] Print-only mode — no changes written.");
                    _outputLog.Add(image.GetInfo());
                }
            });
        }
        catch (Exception ex)
        {
            _outputLog.Add($"[Error] {ex.Message}");
        }
        finally { IsRunning = false; }
    }

    [RelayCommand]
    private async Task GrowFilesystemAsync()
    {
        if (!ValidateImagePath()) return;
        IsRunning = true;
        try
        {
            await Task.Run(() =>
            {
                long newSizeBytes = NewSizeInMB * 1024 * 1024;
                using var image = new Ufs2Image(ImagePath);
                image.GrowFs(newSizeBytes, GrowDryRun);
            });
            _outputLog.Add($"[GrowFS] Filesystem grown to {NewSizeInMB} MB{(GrowDryRun ? " (dry run)" : "")}.");
        }
        catch (Exception ex)
        {
            _outputLog.Add($"[Error] {ex.Message}");
        }
        finally { IsRunning = false; }
    }

    [RelayCommand]
    private async Task CheckFilesystemAsync()
    {
        if (!ValidateImagePath()) return;
        IsRunning = true;
        try
        {
            Ufs2Image.FsckResult? result = null;
            await Task.Run(() =>
            {
                using var image = new Ufs2Image(ImagePath);
                result = image.FsckUfs(FsckPreen, FsckDebug);
            });

            if (result != null)
            {
                string summary = $"Filesystem Check Complete\n" +
                    $"  Clean: {result.Clean}\n" +
                    $"  Modified: {result.Modified}\n" +
                    $"  Files: {result.Files}\n" +
                    $"  Directories: {result.Directories}\n" +
                    $"  Used Blocks: {result.UsedBlocks}\n" +
                    $"  Free Blocks: {result.FreeBlocks}\n" +
                    $"  Used Inodes: {result.UsedInodes}\n" +
                    $"  Free Inodes: {result.FreeInodes}";
                FsckResultText = summary;
                _outputLog.Add($"[FsckUFS] {summary}");

                foreach (var msg in result.Messages) _outputLog.Add($"  {msg}");
                foreach (var warn in result.Warnings) _outputLog.Add($"  [Warning] {warn}");
                foreach (var err in result.Errors) _outputLog.Add($"  [ERROR] {err}");
            }
        }
        catch (Exception ex)
        {
            _outputLog.Add($"[Error] {ex.Message}");
        }
        finally { IsRunning = false; }
    }

    private bool ValidateImagePath()
    {
        if (string.IsNullOrWhiteSpace(ImagePath))
        {
            _outputLog.Add("[Error] Please specify an image file path.");
            return false;
        }
        return true;
    }
}
