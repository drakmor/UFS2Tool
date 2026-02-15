// Copyright (c) 2026, SvenGDK
// Licensed under the BSD 2-Clause License. See LICENSE file for details.

using System;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using UFS2Tool.GUI.ViewModels;

namespace UFS2Tool.GUI.Views;

public partial class ContentBrowserView : UserControl
{
    private ContentBrowserViewModel? _currentVm;

    public ContentBrowserView()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_currentVm != null)
        {
            _currentVm.RootNodes.CollectionChanged -= OnRootNodesChanged;
            _currentVm = null;
        }

        if (DataContext is ContentBrowserViewModel vm)
        {
            _currentVm = vm;
            vm.RootNodes.CollectionChanged += OnRootNodesChanged;
        }
    }

    private void OnRootNodesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_currentVm != null)
            SubscribeToNodeExpansion(_currentVm);
    }

    private void SubscribeToNodeExpansion(ContentBrowserViewModel vm)
    {
        foreach (var node in vm.RootNodes)
            SubscribeNodeExpansion(node, vm);
    }

    private void SubscribeNodeExpansion(FileTreeNodeItem node, ContentBrowserViewModel vm)
    {
        node.PropertyChanged -= OnNodePropertyChanged;
        node.PropertyChanged += OnNodePropertyChanged;
    }

    private async void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        try
        {
            if (e.PropertyName == nameof(FileTreeNodeItem.IsExpanded) &&
                sender is FileTreeNodeItem item && item.IsExpanded && !item.IsLoaded && item.IsDirectory &&
                _currentVm != null)
            {
                await _currentVm.ExpandNodeAsync(item);
                // Subscribe to newly loaded children
                foreach (var child in item.Children)
                    SubscribeNodeExpansion(child, _currentVm);
            }
        }
        catch (Exception)
        {
            // Errors during node expansion are already logged by the ViewModel
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

        if (files.Count > 0 && DataContext is ContentBrowserViewModel vm)
        {
            vm.ImagePath = files[0].Path.LocalPath;
        }
    }

    /// <summary>
    /// Extract the selected node to a user-chosen folder.
    /// </summary>
    private async void ExtractSelected_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ContentBrowserViewModel vm || vm.SelectedNode == null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
        });

        if (folders.Count > 0)
        {
            await vm.ExtractNodeAsync(vm.SelectedNode, folders[0].Path.LocalPath);
        }
    }

    /// <summary>
    /// Add a file to the UFS image at the selected directory (or root).
    /// </summary>
    private async void AddFile_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ContentBrowserViewModel vm) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("All files") { Patterns = new[] { "*" } },
            },
        });

        if (files.Count > 0)
        {
            string targetDir = GetTargetDirectoryPath(vm);
            await vm.AddToImageAsync(targetDir, files[0].Path.LocalPath);
        }
    }

    /// <summary>
    /// Add a folder to the UFS image at the selected directory (or root).
    /// </summary>
    private async void AddFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ContentBrowserViewModel vm) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
        });

        if (folders.Count > 0)
        {
            string targetDir = GetTargetDirectoryPath(vm);
            await vm.AddToImageAsync(targetDir, folders[0].Path.LocalPath);
        }
    }

    /// <summary>
    /// Get the target directory path for add operations based on the selected node.
    /// </summary>
    private static string GetTargetDirectoryPath(ContentBrowserViewModel vm)
    {
        if (vm.SelectedNode != null && vm.SelectedNode.IsDirectory)
            return vm.SelectedNode.FullPath;
        return "/";
    }

    /// <summary>
    /// Handle DragOver to show the appropriate drag effect.
    /// </summary>
    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.None;
        if (e.DataTransfer.Formats.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.Copy;
        }
    }

    /// <summary>
    /// Handle Drop to add dropped files/folders into the UFS image.
    /// </summary>
    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not ContentBrowserViewModel vm) return;

        if (!e.DataTransfer.Formats.Contains(DataFormat.File)) return;

        string targetDir = GetTargetDirectoryPath(vm);

        foreach (var item in e.DataTransfer.Items)
        {
            if (item.TryGetRaw(DataFormat.File) is Avalonia.Platform.Storage.IStorageItem storageItem)
            {
                string? localPath = storageItem.Path?.LocalPath;
                if (string.IsNullOrEmpty(localPath)) continue;
                await vm.AddToImageAsync(targetDir, localPath);
            }
        }
    }
}
