// Copyright (c) 2026, SvenGDK
// Licensed under the BSD 2-Clause License. See LICENSE file for details.

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using UFS2Tool.GUI.Services;
using UFS2Tool.GUI.ViewModels;
using System.ComponentModel;

namespace UFS2Tool.GUI.Views;

public partial class FilesystemOperationsView : UserControl
{
    public FilesystemOperationsView()
    {
        InitializeComponent();
        UpdateColumnHeaders();
        LocalizationManager.Instance.PropertyChanged += OnLocalizationChanged;
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        LocalizationManager.Instance.PropertyChanged -= OnLocalizationChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "Item" or "CurrentLanguage")
            UpdateColumnHeaders();
    }

    private void UpdateColumnHeaders()
    {
        var loc = LocalizationManager.Instance;
        if (DirectoryGrid.Columns.Count >= 3)
        {
            DirectoryGrid.Columns[0].Header = loc["Name"];
            DirectoryGrid.Columns[1].Header = loc["Inode"];
            DirectoryGrid.Columns[2].Header = loc["Type"];
        }
    }

    private async void BrowseImagePath_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Image files") { Patterns = new[] { "*.img" } },
                new FilePickerFileType("All files") { Patterns = new[] { "*" } },
            },
        });

        if (files.Count > 0 && DataContext is FilesystemOperationsViewModel vm)
        {
            vm.ImagePath = files[0].Path.LocalPath;
        }
    }
}
