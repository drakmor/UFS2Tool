// Copyright (c) 2026, SvenGDK
// Licensed under the BSD 2-Clause License. See LICENSE file for details.

using CommunityToolkit.Mvvm.ComponentModel;

namespace UFS2Tool.GUI.Models;

public partial class PS5BatchItem : ObservableObject
{
    [ObservableProperty]
    private string _inputDirectory = "";

    [ObservableProperty]
    private string _outputImagePath = "";

    [ObservableProperty]
    private string _status = "";
}
