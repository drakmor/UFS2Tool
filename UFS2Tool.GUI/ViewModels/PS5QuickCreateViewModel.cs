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

public partial class PS5QuickCreateViewModel : ViewModelBase
{
    private readonly ObservableCollection<string> _outputLog;

    [ObservableProperty]
    private string _inputDirectory = "";

    [ObservableProperty]
    private string _imagePath = "";

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

    public PS5QuickCreateViewModel(ObservableCollection<string> outputLog)
    {
        _outputLog = outputLog;
    }

    [RelayCommand]
    private async Task CreatePS5ImageAsync()
    {
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
            if (UseMakefs)
            {
                _outputLog.Add($"[PS5] Command: makefs -S 4096 -t ffs -o version=2,minfree=0,softupdates=0,optimization=space {ImagePath} {InputDirectory}");

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

                    creator.InputDirectory = InputDirectory;
                    creator.MakeFsImage(ImagePath, InputDirectory);
                });
            }
            else
            {
                _outputLog.Add($"[PS5] Command: newfs -D {InputDirectory} {ImagePath}");

                await Task.Run(() =>
                {
                    using var logWriter = new LogTextWriter(_outputLog);
                    var creator = new Ufs2ImageCreator
                    {
                        FilesystemFormat = 2,
                        Output = logWriter,
                        ErrorOutput = logWriter,
                    };

                    creator.InputDirectory = InputDirectory;
                    creator.CreateImageFromDirectory(ImagePath, InputDirectory);
                });
            }

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
}
