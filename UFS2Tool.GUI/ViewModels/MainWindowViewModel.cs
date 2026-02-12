// Copyright (c) 2026, SvenGDK
// Licensed under the BSD 2-Clause License. See LICENSE file for details.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
namespace UFS2Tool.GUI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentTab;

    [ObservableProperty]
    private int _selectedTabIndex;

    public ObservableCollection<string> OutputLog { get; } = new();

    public CreateFilesystemViewModel CreateFilesystemTab { get; }
    public FilesystemOperationsViewModel FilesystemOperationsTab { get; }
    public MaintenanceViewModel MaintenanceTab { get; }
    public DeviceMountViewModel DeviceMountTab { get; }
    public PS5QuickCreateViewModel PS5QuickCreateTab { get; }
    public SettingsViewModel SettingsTab { get; }

    public MainWindowViewModel()
    {
        CreateFilesystemTab = new CreateFilesystemViewModel(OutputLog);
        FilesystemOperationsTab = new FilesystemOperationsViewModel(OutputLog);
        MaintenanceTab = new MaintenanceViewModel(OutputLog);
        DeviceMountTab = new DeviceMountViewModel(OutputLog);
        PS5QuickCreateTab = new PS5QuickCreateViewModel(OutputLog);
        SettingsTab = new SettingsViewModel();
        _currentTab = CreateFilesystemTab;
    }

    [RelayCommand]
    public void ClearLog()
    {
        OutputLog.Clear();
    }
}
