// Copyright (c) 2026, SvenGDK
// Licensed under the BSD 2-Clause License. See LICENSE file for details.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using UFS2Tool;
namespace UFS2Tool.GUI.ViewModels;

public partial class CreateFilesystemViewModel : ViewModelBase
{
    private readonly ObservableCollection<string> _outputLog;

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
    private bool _softUpdates;

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

    [RelayCommand]
    private async Task CreateFilesystemAsync()
    {
        if (string.IsNullOrWhiteSpace(ImagePath))
        {
            _outputLog.Add("[Error] Please specify an image file path.");
            return;
        }

        IsRunning = true;
        _outputLog.Add($"[NewFS] Creating UFS{FilesystemFormat} filesystem: {ImagePath}");

        try
        {
            await Task.Run(() =>
            {
                var creator = new Ufs2ImageCreator
                {
                    FilesystemFormat = FilesystemFormat,
                    BlockSize = BlockSize,
                    FragmentSize = FragmentSize,
                    SectorSize = SectorSize,
                    VolumeName = VolumeName ?? "",
                    MinFreePercent = MinFreePercent,
                    OptimizationPreference = OptimizationPreference ?? "time",
                    BytesPerInode = BytesPerInode,
                    MaxContig = MaxContig,
                    SoftUpdates = SoftUpdates,
                    SoftUpdatesJournal = SoftUpdatesJournal,
                    TrimEnabled = TrimEnabled,
                    EraseContents = EraseContents,
                    DryRun = DryRun,
                };

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
