// Copyright (c) 2026, SvenGDK
// Licensed under the BSD 2-Clause License. See LICENSE file for details.

using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Interactivity;
using UFS2Tool.GUI.ViewModels;

namespace UFS2Tool.GUI.Views;

public partial class OutputLogWindow : Window
{
    public OutputLogWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.OutputLog.CollectionChanged += OnOutputLogChanged;
            Closed += (_, _) => vm.OutputLog.CollectionChanged -= OnOutputLogChanged;

            // Scroll to the latest entry when the window opens
            if (LogList.ItemCount > 0)
                LogList.ScrollIntoView(LogList.ItemCount - 1);
        }
    }

    private void OnOutputLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Replace)
        {
            var list = LogList;
            if (list.ItemCount > 0)
                list.ScrollIntoView(list.ItemCount - 1);
        }
    }
}
