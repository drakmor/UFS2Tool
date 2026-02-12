// Copyright (c) 2026, SvenGDK
// Licensed under the BSD 2-Clause License. See LICENSE file for details.

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using UFS2Tool.GUI.ViewModels;

namespace UFS2Tool.GUI.Views;

public partial class CreateFilesystemView : UserControl
{
    public CreateFilesystemView()
    {
        InitializeComponent();
    }

    private async void BrowseImagePath_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            DefaultExtension = "img",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Image files") { Patterns = new[] { "*.img" } },
                new FilePickerFileType("All files") { Patterns = new[] { "*" } },
            },
        });

        if (file != null && DataContext is CreateFilesystemViewModel vm)
        {
            vm.ImagePath = file.Path.LocalPath;
        }
    }

    private async void BrowseInputDirectory_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
        });

        if (folders.Count > 0 && DataContext is CreateFilesystemViewModel vm)
        {
            vm.InputDirectory = folders[0].Path.LocalPath;
        }
    }
}
