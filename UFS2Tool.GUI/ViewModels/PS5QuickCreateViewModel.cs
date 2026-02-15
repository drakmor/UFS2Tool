// Copyright (c) 2026, SvenGDK
// Licensed under the BSD 2-Clause License. See LICENSE file for details.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UFS2Tool;
using UFS2Tool.GUI.Models;
using UFS2Tool.GUI.Services;
namespace UFS2Tool.GUI.ViewModels;

public partial class PS5QuickCreateViewModel : ViewModelBase
{
    private readonly ObservableCollection<string> _outputLog;

    // Single mode properties
    [ObservableProperty]
    private string _inputDirectory = "";

    [ObservableProperty]
    private string _imagePath = "";

    // Shared
    [ObservableProperty]
    private bool _useMakefs = true;

    public bool UseNewfs
    {
        get => !UseMakefs;
        set => UseMakefs = !value;
    }

    partial void OnUseMakefsChanged(bool value)
    {
        OnPropertyChanged(nameof(UseNewfs));
    }

    [ObservableProperty]
    private bool _isRunning;

    // Batch mode properties
    [ObservableProperty]
    private bool _isBatchMode;

    public ObservableCollection<PS5BatchItem> BatchItems { get; } = new();

    [ObservableProperty]
    private PS5BatchItem? _selectedBatchItem;

    [ObservableProperty]
    private string _batchOutputDirectory = "";

    [ObservableProperty]
    private string _batchProgress = "";

    public PS5QuickCreateViewModel(ObservableCollection<string> outputLog)
    {
        _outputLog = outputLog;
    }

    [RelayCommand]
    private void AddBatchItem()
    {
        BatchItems.Add(new PS5BatchItem());
    }

    [RelayCommand]
    private void RemoveBatchItem(PS5BatchItem? item)
    {
        if (item != null)
            BatchItems.Remove(item);
    }

    [RelayCommand]
    private void ClearBatchItems()
    {
        BatchItems.Clear();
    }

    [RelayCommand]
    private async Task CreatePS5ImageAsync()
    {
        if (IsBatchMode)
        {
            await CreateBatchImagesAsync();
            return;
        }

        if (string.IsNullOrWhiteSpace(InputDirectory))
        {
            _outputLog.Add($"[Error] {Loc["PS5ErrorInputDirectory"]}");
            return;
        }

        if (string.IsNullOrWhiteSpace(ImagePath))
        {
            _outputLog.Add($"[Error] {Loc["PS5ErrorFilename"]}");
            return;
        }

        IsRunning = true;
        _outputLog.Add($"[PS5] Creating PS5 filesystem image using {(UseMakefs ? "makefs" : "newfs")} mode...");
        _outputLog.Add($"[PS5] Input directory: {InputDirectory}");
        _outputLog.Add($"[PS5] Output image: {ImagePath}");

        try
        {
            await CreateSingleImageAsync(InputDirectory, ImagePath);
            _outputLog.Add($"[PS5] {Loc["PS5Success"]}");
        }
        catch (Exception ex)
        {
            _outputLog.Add($"[Error] {ex.Message}");
            if (ex.InnerException != null)
                _outputLog.Add($"  Detail: {ex.InnerException.Message}");
        }
        finally
        {
            IsRunning = false;
        }
    }

    private async Task CreateBatchImagesAsync()
    {
        if (BatchItems.Count == 0)
        {
            _outputLog.Add($"[Error] {Loc["PS5BatchErrorNoItems"]}");
            return;
        }

        if (string.IsNullOrWhiteSpace(BatchOutputDirectory))
        {
            _outputLog.Add($"[Error] {Loc["PS5BatchErrorNoOutputDir"]}");
            return;
        }

        // Validate all items have input directories
        for (int i = 0; i < BatchItems.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(BatchItems[i].InputDirectory))
            {
                _outputLog.Add($"[Error] {Loc["PS5BatchErrorItemMissing"]} (#{i + 1})");
                return;
            }

            if (!Directory.Exists(BatchItems[i].InputDirectory))
            {
                _outputLog.Add($"[Error] {Loc["PS5BatchErrorDirNotFound"]}: {BatchItems[i].InputDirectory}");
                return;
            }
        }

        IsRunning = true;
        int total = BatchItems.Count;
        int succeeded = 0;
        int failed = 0;

        _outputLog.Add($"[PS5 Batch] {Loc["PS5BatchStarting"]} ({total} items)");

        try
        {
            if (!Directory.Exists(BatchOutputDirectory))
                Directory.CreateDirectory(BatchOutputDirectory);

            // Track used output paths to detect collisions
            var usedOutputPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < BatchItems.Count; i++)
            {
                var item = BatchItems[i];
                BatchProgress = $"{i + 1} / {total}";
                item.Status = Loc["PS5BatchStatusProcessing"];

                string dirName = Path.GetFileName(item.InputDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrEmpty(dirName))
                    dirName = $"image_{i + 1}";
                string outputPath = !string.IsNullOrWhiteSpace(item.OutputImagePath)
                    ? item.OutputImagePath
                    : Path.Combine(BatchOutputDirectory, dirName + ".ffpkg");

                // Resolve to full path for consistent collision detection
                outputPath = Path.GetFullPath(outputPath);

                // Deduplicate: append _2, _3, etc. if path already used
                if (!usedOutputPaths.Add(outputPath))
                {
                    string dir = Path.GetDirectoryName(outputPath) ?? BatchOutputDirectory;
                    string nameWithoutExt = Path.GetFileNameWithoutExtension(outputPath);
                    string ext = Path.GetExtension(outputPath);
                    int suffix = 2;
                    do
                    {
                        outputPath = Path.Combine(dir, $"{nameWithoutExt}_{suffix}{ext}");
                        suffix++;
                    } while (!usedOutputPaths.Add(outputPath));
                }

                // Ensure parent directory exists for custom output paths
                string? parentDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                    Directory.CreateDirectory(parentDir);

                _outputLog.Add($"[PS5 Batch {i + 1}/{total}] {item.InputDirectory} → {outputPath}");

                try
                {
                    await CreateSingleImageAsync(item.InputDirectory, outputPath);
                    item.Status = Loc["PS5BatchStatusDone"];
                    succeeded++;
                    _outputLog.Add($"[PS5 Batch {i + 1}/{total}] {Loc["PS5Success"]}");
                }
                catch (Exception ex)
                {
                    item.Status = Loc["PS5BatchStatusFailed"];
                    failed++;
                    _outputLog.Add($"[Error] {ex.Message}");
                    if (ex.InnerException != null)
                        _outputLog.Add($"  Detail: {ex.InnerException.Message}");
                }
            }

            BatchProgress = $"{total} / {total}";
            _outputLog.Add($"[PS5 Batch] {Loc["PS5BatchComplete"]}: {succeeded} {Loc["PS5BatchSucceeded"]}, {failed} {Loc["PS5BatchFailed"]}");
        }
        catch (Exception ex)
        {
            _outputLog.Add($"[Error] {ex.Message}");
        }
        finally
        {
            IsRunning = false;
        }
    }

    private async Task CreateSingleImageAsync(string inputDir, string outputPath)
    {
        if (UseMakefs)
        {
            _outputLog.Add($"[PS5] Command: makefs -S 4096 -t ffs -o version=2,minfree=0,softupdates=0,optimization=space {outputPath} {inputDir}");

            await Task.Run(() =>
            {
                using var logWriter = new LogTextWriter(_outputLog);
                var creator = new Ufs2ImageCreator
                {
                    FilesystemFormat = 2,
                    FragmentSize = 4096,
                    SectorSize = 4096,
                    MinFreePercent = 0,
                    SoftUpdates = false,
                    OptimizationPreference = "space",
                    Output = logWriter,
                    ErrorOutput = logWriter,
                };

                creator.InputDirectory = inputDir;
                creator.MakeFsImage(outputPath, inputDir);
            });
        }
        else
        {
            _outputLog.Add($"[PS5] Command: newfs -D {inputDir} {outputPath}");

            await Task.Run(() =>
            {
                using var logWriter = new LogTextWriter(_outputLog);
                var creator = new Ufs2ImageCreator
                {
                    FilesystemFormat = 2,
                    Output = logWriter,
                    ErrorOutput = logWriter,
                };

                creator.InputDirectory = inputDir;
                creator.CreateImageFromDirectory(outputPath, inputDir);
            });
        }
    }
}
