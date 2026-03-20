// Copyright (c) 2026, SvenGDK
// Licensed under the BSD 2-Clause License. See LICENSE file for details.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using UFS2Tool;
using UFS2Tool.GUI.Services;
namespace UFS2Tool.GUI.ViewModels;

public partial class CreateFilesystemViewModel : ViewModelBase
{
    private readonly ObservableCollection<string> _outputLog;
    private bool _softUpdatesTouched;
    private bool _optimizationTouched;
    private bool _suppressTouchTracking;

    private string GetEffectiveOptimizationPreference()
    {
        if (_optimizationTouched && !string.IsNullOrWhiteSpace(OptimizationPreference))
            return OptimizationPreference;

        return string.IsNullOrWhiteSpace(InputDirectory)
            ? (MinFreePercent < 8 ? "space" : "time")
            : "time";
    }

    private bool GetEffectiveSoftUpdates()
    {
        if (_softUpdatesTouched)
            return SoftUpdates;

        return string.IsNullOrWhiteSpace(InputDirectory) && FilesystemFormat > 1;
    }

    [ObservableProperty]
    private string _imagePath = "";

    [ObservableProperty]
    private int _filesystemFormat = 2;

    /// <summary>
    /// ComboBox index: 0=UFS1, 1=UFS2. Maps to/from FilesystemFormat (1 or 2).
    /// </summary>
    public int FilesystemFormatIndex
    {
        get => FilesystemFormat - 1;
        set => FilesystemFormat = value + 1;
    }

    partial void OnFilesystemFormatChanged(int value)
    {
        OnPropertyChanged(nameof(FilesystemFormatIndex));
        if (!_softUpdatesTouched)
        {
            _suppressTouchTracking = true;
            SoftUpdates = string.IsNullOrWhiteSpace(InputDirectory) && value > 1;
            _suppressTouchTracking = false;
        }
    }

    [ObservableProperty]
    private int _blockSize = 32768;

    [ObservableProperty]
    private int _fragmentSize = 4096;

    [ObservableProperty]
    private int _sectorSize = 512;

    [ObservableProperty]
    private string _volumeName = "";

    [ObservableProperty]
    private int _minFreePercent = 8;

    [ObservableProperty]
    private string _optimizationPreference = "time";

    [ObservableProperty]
    private int _bytesPerInode;

    [ObservableProperty]
    private int _maxContig;

    [ObservableProperty]
    private long _sizeInMB = 100;

    [ObservableProperty]
    private string _inputDirectory = "";

    [ObservableProperty]
    private bool _softUpdates = true;

    [ObservableProperty]
    private bool _softUpdatesJournal;

    [ObservableProperty]
    private bool _trimEnabled;

    [ObservableProperty]
    private bool _eraseContents;

    [ObservableProperty]
    private bool _dryRun;

    [ObservableProperty]
    private bool _isRunning;

    public CreateFilesystemViewModel(ObservableCollection<string> outputLog)
    {
        _outputLog = outputLog;
    }

    partial void OnSoftUpdatesChanged(bool value)
    {
        if (!_suppressTouchTracking)
            _softUpdatesTouched = true;
    }

    partial void OnOptimizationPreferenceChanged(string value)
    {
        if (!_suppressTouchTracking)
            _optimizationTouched = true;
    }

    partial void OnMinFreePercentChanged(int value)
    {
        if (!_optimizationTouched)
        {
            _suppressTouchTracking = true;
            OptimizationPreference = string.IsNullOrWhiteSpace(InputDirectory)
                ? (value < 8 ? "space" : "time")
                : "time";
            _suppressTouchTracking = false;
        }
    }

    partial void OnInputDirectoryChanged(string value)
    {
        bool useMakeFs = !string.IsNullOrWhiteSpace(value);

        if (!_softUpdatesTouched)
        {
            _suppressTouchTracking = true;
            SoftUpdates = !useMakeFs && FilesystemFormat > 1;
            _suppressTouchTracking = false;
        }

        if (!_optimizationTouched)
        {
            _suppressTouchTracking = true;
            OptimizationPreference = useMakeFs
                ? "time"
                : (MinFreePercent < 8 ? "space" : "time");
            _suppressTouchTracking = false;
        }
    }

    [RelayCommand]
    private async Task CreateFilesystemAsync()
    {
        if (string.IsNullOrWhiteSpace(ImagePath))
        {
            _outputLog.Add("[Error] Please specify an image file path.");
            return;
        }

        if (string.IsNullOrWhiteSpace(InputDirectory) && SizeInMB <= 0)
        {
            _outputLog.Add("[Error] Image size must be greater than 0 MB.");
            return;
        }

        IsRunning = true;
        string effectiveOptimization = GetEffectiveOptimizationPreference();
        bool effectiveSoftUpdates = GetEffectiveSoftUpdates();
        _outputLog.Add($"[NewFS] Creating UFS{FilesystemFormat} filesystem: {ImagePath}");
        _outputLog.Add($"[NewFS] Parameters: BlockSize={BlockSize}, FragSize={FragmentSize}, SectorSize={SectorSize}");
        _outputLog.Add($"[NewFS] Options: MinFree={MinFreePercent}%, Optimization={effectiveOptimization}, SoftUpdates={effectiveSoftUpdates}, Journal={SoftUpdatesJournal}, TRIM={TrimEnabled}");
        if (!string.IsNullOrWhiteSpace(VolumeName))
            _outputLog.Add($"[NewFS] Volume name: {VolumeName}");
        if (!string.IsNullOrWhiteSpace(InputDirectory))
            _outputLog.Add($"[NewFS] Input directory: {InputDirectory}");
        else
            _outputLog.Add($"[NewFS] Image size: {SizeInMB} MB");
        if (DryRun)
            _outputLog.Add("[NewFS] Dry run mode — no changes will be written.");
        if (EraseContents)
            _outputLog.Add("[NewFS] Erase contents enabled.");

        try
        {
            await Task.Run(() =>
            {
                using var logWriter = new LogTextWriter(_outputLog);
                var creator = new Ufs2ImageCreator
                {
                    FilesystemFormat = FilesystemFormat,
                    BlockSize = BlockSize,
                    FragmentSize = FragmentSize,
                    SectorSize = SectorSize,
                    VolumeName = VolumeName ?? "",
                    MinFreePercent = MinFreePercent,
                    BytesPerInode = BytesPerInode,
                    MaxContig = MaxContig,
                    SoftUpdatesJournal = SoftUpdatesJournal,
                    TrimEnabled = TrimEnabled,
                    EraseContents = EraseContents,
                    DryRun = DryRun,
                    Output = logWriter,
                    ErrorOutput = logWriter,
                };

                if (_optimizationTouched && !string.IsNullOrWhiteSpace(OptimizationPreference))
                    creator.OptimizationPreference = OptimizationPreference;
                if (_softUpdatesTouched)
                    creator.SoftUpdates = SoftUpdates;

                if (!string.IsNullOrWhiteSpace(InputDirectory))
                {
                    creator.InputDirectory = InputDirectory;
                    creator.MakeFsImage(ImagePath, InputDirectory);
                }
                else
                {
                    long totalBytes = SizeInMB * 1024 * 1024;
                    creator.CreateImage(ImagePath, totalBytes);
                }
            });

            _outputLog.Add("[NewFS] Filesystem created successfully.");
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
}
