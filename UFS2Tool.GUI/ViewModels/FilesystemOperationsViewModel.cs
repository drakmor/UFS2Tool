// Copyright (c) 2026, SvenGDK
// Licensed under the BSD 2-Clause License. See LICENSE file for details.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using UFS2Tool;

namespace UFS2Tool.GUI.ViewModels;

public partial class DirectoryEntryItem : ObservableObject
{
    public string Name { get; set; } = "";
    public uint InodeNumber { get; set; }
    public byte FileType { get; set; }
    public string FileTypeName => FileType switch
    {
        4 => "Directory",
        8 => "File",
        10 => "Symlink",
        _ => $"Type({FileType})"
    };
}

public partial class FilesystemOperationsViewModel : ViewModelBase
{
    private readonly ObservableCollection<string> _outputLog;

    [ObservableProperty]
    private string _imagePath = "";

    [ObservableProperty]
    private string _fsPath = "/";

    [ObservableProperty]
    private string _localPath = "";

    [ObservableProperty]
    private string _outputDirectory = "";

    [ObservableProperty]
    private string _chmodMode = "755";

    [ObservableProperty]
    private bool _chmodRecursive;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _superblockInfo = "";

    public ObservableCollection<DirectoryEntryItem> DirectoryEntries { get; } = new();

    public FilesystemOperationsViewModel(ObservableCollection<string> outputLog)
    {
        _outputLog = outputLog;
    }

    [RelayCommand]
    private async Task ShowInfoAsync()
    {
        if (!ValidateImagePath()) return;
        IsRunning = true;
        try
        {
            string info = "";
            await Task.Run(() =>
            {
                using var image = new Ufs2Image(ImagePath, readOnly: true);
                info = image.GetInfo();
            });
            SuperblockInfo = info;
            _outputLog.Add("[Info] Filesystem information loaded.");
            _outputLog.Add(info);
        }
        catch (Exception ex)
        {
            _outputLog.Add($"[Error] {ex.Message}");
        }
        finally { IsRunning = false; }
    }

    [RelayCommand]
    private async Task ListDirectoryAsync()
    {
        if (!ValidateImagePath()) return;
        IsRunning = true;
        try
        {
            await Task.Run(() =>
            {
                using var image = new Ufs2Image(ImagePath, readOnly: true);
                uint dirInode = string.IsNullOrWhiteSpace(FsPath) || FsPath == "/"
                    ? Ufs2Constants.RootInode
                    : image.ResolvePath(FsPath);
                var entries = image.ListDirectory(dirInode);

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    DirectoryEntries.Clear();
                    foreach (var entry in entries)
                    {
                        if (entry.Inode == 0) continue;
                        DirectoryEntries.Add(new DirectoryEntryItem
                        {
                            Name = entry.Name,
                            InodeNumber = entry.Inode,
                            FileType = entry.FileType,
                        });
                    }
                });
            });
            _outputLog.Add($"[LS] Listed directory: {FsPath}");
        }
        catch (Exception ex)
        {
            _outputLog.Add($"[Error] {ex.Message}");
        }
        finally { IsRunning = false; }
    }

    [RelayCommand]
    private async Task ExtractAsync()
    {
        if (!ValidateImagePath()) return;
        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            _outputLog.Add("[Error] Please specify an output directory.");
            return;
        }
        IsRunning = true;
        try
        {
            await Task.Run(() =>
            {
                using var image = new Ufs2Image(ImagePath, readOnly: true);
                image.Extract(FsPath ?? "/", OutputDirectory);
            });
            _outputLog.Add($"[Extract] Extracted '{FsPath}' to '{OutputDirectory}'.");
        }
        catch (Exception ex)
        {
            _outputLog.Add($"[Error] {ex.Message}");
        }
        finally { IsRunning = false; }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (!ValidateImagePath()) return;
        if (string.IsNullOrWhiteSpace(LocalPath))
        {
            _outputLog.Add("[Error] Please specify a local file or directory path.");
            return;
        }
        IsRunning = true;
        try
        {
            await Task.Run(() =>
            {
                using var image = new Ufs2Image(ImagePath);
                image.Add(FsPath ?? "/", LocalPath);
            });
            _outputLog.Add($"[Add] Added '{LocalPath}' to '{FsPath}'.");
        }
        catch (Exception ex)
        {
            _outputLog.Add($"[Error] {ex.Message}");
        }
        finally { IsRunning = false; }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (!ValidateImagePath()) return;
        if (string.IsNullOrWhiteSpace(FsPath) || FsPath == "/")
        {
            _outputLog.Add("[Error] Please specify a filesystem path to delete.");
            return;
        }
        IsRunning = true;
        try
        {
            await Task.Run(() =>
            {
                using var image = new Ufs2Image(ImagePath);
                image.Delete(FsPath);
            });
            _outputLog.Add($"[Delete] Deleted '{FsPath}'.");
        }
        catch (Exception ex)
        {
            _outputLog.Add($"[Error] {ex.Message}");
        }
        finally { IsRunning = false; }
    }

    [RelayCommand]
    private async Task ReplaceAsync()
    {
        if (!ValidateImagePath()) return;
        if (string.IsNullOrWhiteSpace(FsPath) || string.IsNullOrWhiteSpace(LocalPath))
        {
            _outputLog.Add("[Error] Please specify both a filesystem path and a local source path.");
            return;
        }
        IsRunning = true;
        try
        {
            await Task.Run(() =>
            {
                using var image = new Ufs2Image(ImagePath);
                image.Replace(FsPath, LocalPath);
            });
            _outputLog.Add($"[Replace] Replaced '{FsPath}' with '{LocalPath}'.");
        }
        catch (Exception ex)
        {
            _outputLog.Add($"[Error] {ex.Message}");
        }
        finally { IsRunning = false; }
    }

    [RelayCommand]
    private async Task ChmodAsync()
    {
        if (!ValidateImagePath()) return;
        if (string.IsNullOrWhiteSpace(ChmodMode))
        {
            _outputLog.Add("[Error] Please specify a chmod mode (e.g., 755).");
            return;
        }
        IsRunning = true;
        try
        {
            await Task.Run(() =>
            {
                ushort mode = Convert.ToUInt16(ChmodMode, 8);
                using var image = new Ufs2Image(ImagePath);
                if (ChmodRecursive)
                {
                    // Apply same mode to both files and directories
                    image.ChmodAll(mode, mode);
                }
                else
                {
                    image.Chmod(FsPath ?? "/", mode);
                }
            });
            _outputLog.Add($"[Chmod] Changed permissions of '{FsPath}' to {ChmodMode}{(ChmodRecursive ? " (recursive)" : "")}.");
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
