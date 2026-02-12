// Copyright (c) 2026, SvenGDK
// Licensed under the BSD 2-Clause License. See LICENSE file for details.

using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using UFS2Tool.GUI.ViewModels;

namespace UFS2Tool.GUI.Views;

public partial class MainWindow : Window
{
    private OutputLogWindow? _logWindow;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void DetachLog_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        if (_logWindow != null)
        {
            _logWindow.Activate();
            return;
        }

        _logWindow = new OutputLogWindow
        {
            DataContext = vm
        };

        OutputLogPanel.IsVisible = false;

        void OnLogWindowClosed(object? s, EventArgs args)
        {
            _logWindow!.Closed -= OnLogWindowClosed;
            OutputLogPanel.IsVisible = true;
            _logWindow = null;
        }

        _logWindow.Closed += OnLogWindowClosed;

        _logWindow.Show();
    }
}