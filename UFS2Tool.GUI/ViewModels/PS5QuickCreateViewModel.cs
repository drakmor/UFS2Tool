// Copyright (c) 2026, SvenGDK
// Licensed under the BSD 2-Clause License. See LICENSE file for details.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using UFS2Tool;
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

        try
        {
            if (UseMakefs)
            {
                _outputLog.Add($"[PS5] makefs -S 4096 -t ffs -o version=2,minfree=0,softupdates=0,optimization=space {ImagePath} {InputDirectory}");

                await Task.Run(() =>
                {
                    var creator = new Ufs2ImageCreator
                    {
                        FilesystemFormat = 2,
                        FragmentSize = 4096,
                        SectorSize = 4096,
                        MinFreePercent = 0,
                        SoftUpdates = false,
                        OptimizationPreference = "space",
                    };

                    creator.InputDirectory = InputDirectory;
                    creator.MakeFsImage(ImagePath, InputDirectory);
                });
            }
            else
            {
                _outputLog.Add($"[PS5] newfs -D {InputDirectory} {ImagePath}");

                await Task.Run(() =>
                {
                    var creator = new Ufs2ImageCreator
                    {
                        FilesystemFormat = 2,
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
